package main

import (
	"errors"
	"fmt"
	"log/slog"
	"runtime"
	"sort"
	"strings"
	"sync"
	"time"

	"rafaelmartins.com/p/usbhid"
)

const (
	// Usage Page (Vendor Defined 0xFF00)
	UsagePageVendorDefined uint16 = 0xff00

	// Vendor-defined usages
	UsageCharger           uint16 = 0x14
	UsageHidIo             uint16 = 0x10
	UsageRequestReportById uint16 = 0x11

	// RPC Timeout Configuration
	RpcTimeoutGetSettings time.Duration = 10 * time.Second
	RpcTimeoutSetSettings time.Duration = 0 // Fire-and-forget, timeout ignored
	RpcTimeoutSaveConfig  time.Duration = 0 // Fire-and-forget, timeout ignored
	RpcTimeoutGetKeymap   time.Duration = 10 * time.Second
	RpcTimeoutSetKeymap   time.Duration = 10 * time.Second

	// Report IDs
	ReportIdHidIo   byte = 0x12
	ReportIdCharger byte = 0x90

	// Report commands
	ReportRequestById byte = 0x91
)

// MagicStickDevice bundles the command and charger interfaces of one physical device
// and manages background workers for periodic requests and input report handling.
type MagicStickDevice struct {
	cmdInterface     *usbhid.Device
	chargerInterface *usbhid.Device
	hidioInterface   *usbhid.Device

	// background control
	backgroundOnce sync.Once
	stopCh         chan struct{}
	backgroundWg   sync.WaitGroup

	// RPC connection for event monitoring
	rpcConnection *MagicStickRPC

	// callbacks
	batteryCallback    func(raw []byte)
	disconnectCallback func(error)                       // Called when device disconnects
	rpcEventCallback   func(name string, payload string) // Called when RPC events are received
	disconnectNotified bool                              // Prevents multiple disconnect notifications
}

// MagicStickInfo represents an enumerated candidate device before opening.
type MagicStickInfo struct {
	Index        int
	VendorID     uint16
	ProductID    uint16
	Product      string // Device Model (from Product())
	Serial       string // Device Serial (from SerialNumber())
	Manufacturer string
	// Interfaces found (paths for debugging/selection)
	CommandPath string
	ChargerPath string
	HidIoPath   string

	commandDev *usbhid.Device
	chargerDev *usbhid.Device
	hidioDev   *usbhid.Device
}

// EnumerateMagicStick scans the system for Magic Stick devices and returns
// one entry per physical device that has either command or charger interface present.
// Results are sorted by serial for stable ordering.
func (ms *MagicStickDevice) EnumerateDevices() ([]*MagicStickInfo, error) {
	slog.Debug("EnumerateDevices()")

	const vendorId uint16 = 0x2e8a
	const productId uint16 = 0xc010

	devices, err := usbhid.Enumerate(func(d *usbhid.Device) bool {
		return d.VendorId() == vendorId && d.ProductId() == productId
	})
	if err != nil {
		return nil, err
	}
	slog.Debug("Found raw USB devices", "count", len(devices), "vendor", fmt.Sprintf("0x%04x", vendorId), "product", fmt.Sprintf("0x%04x", productId))
	if len(devices) == 0 {
		return []*MagicStickInfo{}, nil
	}

	// Group by serial when available; otherwise group by product string.
	byKey := map[string]*MagicStickInfo{}
	for _, d := range devices {
		key := d.SerialNumber()
		if key == "" {
			key = fmt.Sprintf("%s|%04x:%04x", d.Product(), d.VendorId(), d.ProductId())
		}
		ms := byKey[key]
		if ms == nil {
			ms = &MagicStickInfo{
				VendorID:     d.VendorId(),
				ProductID:    d.ProductId(),
				Product:      d.Product(),
				Serial:       d.SerialNumber(),
				Manufacturer: d.Manufacturer(),
			}
			byKey[key] = ms
		}
		// Update descriptive fields if missing and available from this interface
		if ms.Product == "" {
			ms.Product = d.Product()
		}
		if ms.Serial == "" {
			ms.Serial = d.SerialNumber()
		}
		if ms.Manufacturer == "" {
			ms.Manufacturer = d.Manufacturer()
		}
		usage := d.Usage()
		devicePath := d.Path()
		slog.Debug("Device interface", "serial", d.SerialNumber(), "usage", fmt.Sprintf("0x%04x", usage), "path", devicePath)

		// On Linux, all interfaces may share the same /dev/hidraw* path
		// We need to handle this by using the same device for all interfaces
		if runtime.GOOS == "linux" && strings.HasPrefix(devicePath, "/dev/hidraw") {
			// On Linux, use the same device path for all interfaces
			// The usbhid library should handle interface selection internally
			slog.Debug("Linux detected - using shared device path", "path", devicePath)
		}

		// On Darwin, all interfaces may share the same IOService path
		// We need to handle this by using the same device for all interfaces
		if runtime.GOOS == "darwin" && strings.Contains(devicePath, "IOService:") {
			// On Darwin, use the same device path for all interfaces
			// The usbhid library should handle interface selection internally
			slog.Debug("Darwin detected - using shared device path", "path", devicePath)
		}

		switch usage {
		case UsageRequestReportById:
			slog.Debug("Found Command interface", "serial", d.SerialNumber())
			ms.CommandPath = devicePath
			ms.commandDev = d
		case UsageCharger:
			slog.Debug("Found Charger interface", "serial", d.SerialNumber())
			ms.ChargerPath = devicePath
			ms.chargerDev = d
		case UsageHidIo:
			slog.Debug("Found HidIo interface", "serial", d.SerialNumber())
			ms.HidIoPath = devicePath
			ms.hidioDev = d
		default:
			slog.Debug("Unknown usage", "usage", fmt.Sprintf("0x%04x", usage), "serial", d.SerialNumber())
		}
	}

	// Linux and Darwin-specific fix: If we only found one interface but it's a composite device,
	// try to create device references for all expected interfaces
	if runtime.GOOS == "linux" || runtime.GOOS == "darwin" {
		for _, deviceInfo := range byKey {
			// Check if we have a device but missing some interfaces
			hasAnyInterface := deviceInfo.commandDev != nil || deviceInfo.chargerDev != nil || deviceInfo.hidioDev != nil
			if hasAnyInterface {
				// Find the first available device to use as a template
				var templateDev *usbhid.Device
				if deviceInfo.commandDev != nil {
					templateDev = deviceInfo.commandDev
				} else if deviceInfo.chargerDev != nil {
					templateDev = deviceInfo.chargerDev
				} else if deviceInfo.hidioDev != nil {
					templateDev = deviceInfo.hidioDev
				}

				if templateDev != nil {
					devicePath := templateDev.Path()
					slog.Debug("Linux composite device fix - using template device", "serial", deviceInfo.Serial, "path", devicePath)

					// Create device references for missing interfaces
					// On Linux, the same physical device can handle multiple interfaces
					if deviceInfo.commandDev == nil {
						slog.Debug("Creating Command interface reference for Linux device", "serial", deviceInfo.Serial)
						deviceInfo.CommandPath = devicePath
						deviceInfo.commandDev = templateDev // Use same device reference
					}
					if deviceInfo.chargerDev == nil {
						slog.Debug("Creating Charger interface reference for Linux device", "serial", deviceInfo.Serial)
						deviceInfo.ChargerPath = devicePath
						deviceInfo.chargerDev = templateDev // Use same device reference
					}
					if deviceInfo.hidioDev == nil {
						slog.Debug("Creating HidIo interface reference for Linux device", "serial", deviceInfo.Serial)
						deviceInfo.HidIoPath = devicePath
						deviceInfo.hidioDev = templateDev // Use same device reference
					}
				}
			}
		}
	}

	// Build slice
	result := make([]*MagicStickInfo, 0, len(byKey))
	for _, v := range byKey {
		result = append(result, v)
	}

	// Sort devices by serial number for consistent ordering
	sort.Slice(result, func(i, j int) bool {
		return result[i].Serial < result[j].Serial
	})

	// Add indices after sorting and print debug
	for idx, v := range result {
		v.Index = idx

		// On Linux and Darwin, if all interfaces share the same path, show them differently in the UI
		if (runtime.GOOS == "linux" || runtime.GOOS == "darwin") && v.CommandPath == v.ChargerPath && v.ChargerPath == v.HidIoPath && v.CommandPath != "" {
			slog.Debug("Device summary", "index", idx, "serial", v.Serial, "shared_path", v.CommandPath)
			slog.Debug("Interface availability", "command", v.commandDev != nil, "charger", v.chargerDev != nil, "hidio", v.hidioDev != nil)
		} else {
			slog.Debug("Final device", "index", idx, "serial", v.Serial, "command", v.commandDev != nil, "charger", v.chargerDev != nil, "hidio", v.hidioDev != nil)
		}
	}
	slog.Debug("Returning MagicStick devices", "count", len(result))
	return result, nil
}

// Open opens the command and charger interfaces (if available). If lock is true,
// the device is exclusively locked so concurrent processes cannot access it.
// This method can be called multiple times to reopen after disconnection.
func (ms *MagicStickDevice) Open(serial string, lock bool) error {
	slog.Debug("Open()")

	if serial == "" {
		slog.Error("Open failed - device serial is empty")
		return errors.New("device serial is empty")
	}

	// Re-enumerate to get fresh device references since the ones from frontend are not exported
	slog.Debug("Re-enumerating to find device", "serial", serial)
	devices, err := ms.EnumerateDevices()
	if err != nil {
		slog.Error("Open failed - re-enumeration failed", "error", err)
		return fmt.Errorf("re-enumeration failed: %w", err)
	}
	slog.Debug("Re-enumeration successful", "device_count", len(devices))

	// Find the device by serial
	var deviceInfo *MagicStickInfo
	for _, d := range devices {
		if d.Serial == serial {
			deviceInfo = d
			break
		}
	}

	if deviceInfo == nil {
		slog.Error("Open failed - device not found during re-enumeration", "serial", serial)
		return fmt.Errorf("device with serial %s not found during re-enumeration", serial)
	}
	slog.Debug("Device found during re-enumeration", "serial", deviceInfo.Serial)

	slog.Debug("Found device during re-enumeration", "command", deviceInfo.CommandPath != "", "charger", deviceInfo.ChargerPath != "", "hidio", deviceInfo.HidIoPath != "")

	// If already opened, close existing device first
	if ms.cmdInterface != nil || ms.chargerInterface != nil || ms.hidioInterface != nil {
		slog.Debug("Open: closing existing device before opening new one")
		ms.closeInternal()
	}

	// Reset disconnect notification flag for new connection
	ms.disconnectNotified = false

	// Clean up any existing background monitoring before starting new one
	slog.Debug("Open: cleaning up any existing background monitoring")
	ms.stopBackground()
	// Reset backgroundOnce to allow restarting background goroutines
	ms.backgroundOnce = sync.Once{}

	// Debug: Log available interfaces
	slog.Debug("Device interfaces", "command", deviceInfo.CommandPath != "", "charger", deviceInfo.ChargerPath != "", "hidio", deviceInfo.HidIoPath != "")

	// On Linux and Darwin, all interfaces share the same device, so we only need to open it once
	if (runtime.GOOS == "linux" || runtime.GOOS == "darwin") && deviceInfo.commandDev == deviceInfo.chargerDev && deviceInfo.chargerDev == deviceInfo.hidioDev {
		slog.Debug("Composite device - opening single device for all interfaces", "platform", runtime.GOOS)
		// Find any available interface to open
		var deviceToOpen *usbhid.Device
		if deviceInfo.commandDev != nil {
			deviceToOpen = deviceInfo.commandDev
		} else if deviceInfo.chargerDev != nil {
			deviceToOpen = deviceInfo.chargerDev
		} else if deviceInfo.hidioDev != nil {
			deviceToOpen = deviceInfo.hidioDev
		}

		if deviceToOpen != nil {
			slog.Debug("Opening Linux composite device")
			if err := deviceToOpen.Open(lock); err != nil {
				slog.Error("Open failed - Linux composite device open failed", "error", err)
				return fmt.Errorf("open Linux composite device failed: %w", err)
			}
			slog.Debug("Linux composite device opened successfully")

			// Use the same device reference for all interfaces
			if deviceInfo.commandDev != nil {
				ms.cmdInterface = deviceToOpen
				slog.Debug("Command interface assigned to composite device")
			}
			if deviceInfo.chargerDev != nil {
				ms.chargerInterface = deviceToOpen
				slog.Debug("Charger interface assigned to composite device")
			}
			if deviceInfo.hidioDev != nil {
				ms.hidioInterface = deviceToOpen
				slog.Debug("HidIo interface assigned to composite device")
			}
		}
	} else {
		// Non-Linux or separate devices - open each interface individually
		// Open command first if present
		if deviceInfo.commandDev != nil {
			slog.Debug("Opening Command interface")
			if err := deviceInfo.commandDev.Open(lock); err != nil {
				slog.Error("Open failed - command interface open failed", "error", err)
				return fmt.Errorf("open command interface failed: %w", err)
			}
			ms.cmdInterface = deviceInfo.commandDev
			slog.Debug("Command interface opened successfully")
		} else {
			slog.Debug("No Command interface to open")
		}
		// Open charger if present
		if deviceInfo.chargerDev != nil {
			slog.Debug("Opening Charger interface")
			if err := deviceInfo.chargerDev.Open(lock); err != nil {
				slog.Error("Open failed - charger interface open failed", "error", err)
				// best effort: close cmd if we opened it
				if ms.cmdInterface != nil {
					_ = ms.cmdInterface.Close()
					ms.cmdInterface = nil
				}
				return fmt.Errorf("open charger interface failed: %w", err)
			}
			ms.chargerInterface = deviceInfo.chargerDev
			slog.Debug("Charger interface opened successfully")
		} else {
			slog.Debug("No Charger interface to open")
		}
		// Open hidio if present
		if deviceInfo.hidioDev != nil {
			slog.Debug("Opening HidIo interface")
			if err := deviceInfo.hidioDev.Open(lock); err != nil {
				slog.Error("Open failed - hidio interface open failed", "error", err)
				// best effort: close previously opened
				if ms.cmdInterface != nil {
					_ = ms.cmdInterface.Close()
					ms.cmdInterface = nil
				}
				if ms.chargerInterface != nil {
					_ = ms.chargerInterface.Close()
					ms.chargerInterface = nil
				}
				return fmt.Errorf("open hidio interface failed: %w", err)
			}
			ms.hidioInterface = deviceInfo.hidioDev
			slog.Debug("HidIo interface opened successfully")
		} else {
			slog.Debug("No HidIo interface to open")
		}
	}
	if ms.cmdInterface == nil && ms.chargerInterface == nil && ms.hidioInterface == nil {
		slog.Error("Open failed - no usable interfaces to open")
		return errors.New("no usable interfaces to open")
	}
	slog.Debug("Successfully opened interfaces", "command", deviceInfo.CommandPath != "", "charger", deviceInfo.ChargerPath != "", "hidio", deviceInfo.HidIoPath != "")

	// Create persistent RPC connection for event monitoring if HidIo interface is available
	if ms.hidioInterface != nil {
		slog.Debug("Creating RPC connection")
		rpc, err := NewMagicStickRPC(ms.hidioInterface)
		if err != nil {
			slog.Error("Open failed - create RPC connection failed", "error", err)
			return fmt.Errorf("create RPC connection failed: %w", err)
		}
		ms.rpcConnection = rpc
		slog.Debug("RPC connection created successfully")
	} else {
		slog.Debug("No HidIo interface available for RPC connection")
	}

	// Automatically start background monitoring
	slog.Debug("Starting background monitoring")
	ms.startBackground()
	slog.Debug("Background monitoring started")

	slog.Debug("Open completed successfully")
	return nil
}

// Close closes any opened interfaces and stops background monitoring.
func (ms *MagicStickDevice) Close() {
	slog.Debug("Close()")

	slog.Debug("Close: stopping background monitoring")
	ms.stopBackground()

	slog.Debug("Close: stopping RPC connection")
	if ms.rpcConnection != nil {
		ms.rpcConnection.Stop()
		ms.rpcConnection = nil
	}

	slog.Debug("Close: closing device interfaces")
	// Close devices first to unblock any pending GetInputReport() calls
	if ms.chargerInterface != nil && ms.chargerInterface.IsOpen() {
		slog.Debug("Close: closing charger interface")
		_ = ms.chargerInterface.Close()
	}
	if ms.cmdInterface != nil && ms.cmdInterface.IsOpen() {
		slog.Debug("Close: closing command interface")
		_ = ms.cmdInterface.Close()
	}
	if ms.hidioInterface != nil && ms.hidioInterface.IsOpen() {
		slog.Debug("Close: closing hidio interface")
		_ = ms.hidioInterface.Close()
	}
	slog.Debug("Close: all interfaces closed")
}

// closeInternal performs cleanup without triggering callbacks (for internal use)
func (ms *MagicStickDevice) closeInternal() {
	ms.stopBackground()

	// Stop RPC connection
	if ms.rpcConnection != nil {
		ms.rpcConnection.Stop()
		ms.rpcConnection = nil
	}

	// Close devices first to unblock any pending GetInputReport() calls
	if ms.chargerInterface != nil && ms.chargerInterface.IsOpen() {
		_ = ms.chargerInterface.Close()
	}
	if ms.cmdInterface != nil && ms.cmdInterface.IsOpen() {
		_ = ms.cmdInterface.Close()
	}
	if ms.hidioInterface != nil && ms.hidioInterface.IsOpen() {
		_ = ms.hidioInterface.Close()
	}

	// Clear interface references
	ms.cmdInterface = nil
	ms.chargerInterface = nil
	ms.hidioInterface = nil

	// Reset backgroundOnce to allow restarting background goroutines
	ms.backgroundOnce = sync.Once{}
}

// SetBatteryCallback registers a callback that receives battery data from charger reports.
// This supports both standard Go callbacks and Wails events.
func (ms *MagicStickDevice) SetBatteryCallback(cb func(raw []byte)) {
	slog.Debug("SetBatteryCallback()")

	ms.batteryCallback = cb
	// Battery data will be sent via both callback (if set) and Wails events
}

// SetDisconnectCallback registers a callback that is called when the device disconnects.
// The error parameter indicates the reason for disconnection (e.g., USB unplugged).
func (ms *MagicStickDevice) SetDisconnectCallback(cb func(error)) {
	slog.Debug("SetDisconnectCallback()")

	ms.disconnectCallback = cb
}

// SetRpcEventCallback registers a callback that receives RPC events from the device.
// The callback receives the event name and the full JSON payload.
func (ms *MagicStickDevice) SetRpcEventCallback(cb func(name string, payload string)) {
	slog.Debug("SetRpcEventCallback()")

	ms.rpcEventCallback = cb
}

// EnumerateDevices returns a list of available MagicStick devices.
// EnumerateDevices removed; use (*MagicStickDevice).EnumerateMagicStick instead

// notifyDisconnect safely triggers the disconnect callback only once.
func (ms *MagicStickDevice) notifyDisconnect(err error) {
	if ms.disconnectCallback != nil && !ms.disconnectNotified {
		ms.disconnectNotified = true

		// Clean up device interfaces before triggering callback to release exclusive locks
		ms.cleanupOnDisconnect()

		ms.disconnectCallback(err)
	}
}

// cleanupOnDisconnect performs cleanup when device disconnects to release exclusive locks
func (ms *MagicStickDevice) cleanupOnDisconnect() {
	// Stop background monitoring first
	ms.stopBackground()

	// Stop RPC connection
	if ms.rpcConnection != nil {
		ms.rpcConnection.Stop()
		ms.rpcConnection = nil
	}

	// Close all device interfaces to release exclusive locks
	if ms.chargerInterface != nil && ms.chargerInterface.IsOpen() {
		_ = ms.chargerInterface.Close()
		ms.chargerInterface = nil
	}
	if ms.cmdInterface != nil && ms.cmdInterface.IsOpen() {
		_ = ms.cmdInterface.Close()
		ms.cmdInterface = nil
	}
	if ms.hidioInterface != nil && ms.hidioInterface.IsOpen() {
		_ = ms.hidioInterface.Close()
		ms.hidioInterface = nil
	}
}

// RequestBatteryReport manually sends a request for battery report via command interface.
// The report will arrive via the battery callback when received.
func (ms *MagicStickDevice) RequestBatteryReport() error {
	slog.Debug("RequestBatteryReport()")

	if ms.cmdInterface == nil || !ms.cmdInterface.IsOpen() {
		return errors.New("command interface not available")
	}
	payload := []byte{ReportIdCharger, 0, 0, 0, 0, 0, 0, 0}
	outLen := int(ms.cmdInterface.GetOutputReportLength())
	if outLen <= 0 {
		return errors.New("invalid output report length")
	}
	buf := make([]byte, outLen)
	copy(buf, payload)
	return ms.cmdInterface.SetOutputReport(ReportRequestById, buf)
}

// GetSettings retrieves current device settings via RPC.
func (ms *MagicStickDevice) GetSettings() (*GetSettingsReply, error) {
	slog.Debug("GetSettings()")

	if ms.rpcConnection == nil {
		return nil, errors.New("RPC connection not available")
	}
	return ms.rpcConnection.GetSettings(RpcTimeoutGetSettings)
}

// SetSettings updates device settings via RPC (fire-and-forget, no response expected).
func (ms *MagicStickDevice) SetSettings(req *SetSettingsRequest) error {
	slog.Debug("SetSettings()")

	if ms.rpcConnection == nil {
		return errors.New("RPC connection not available")
	}
	// Send the request but don't wait for response (fire-and-forget)
	return ms.rpcConnection.SetSettings(*req, RpcTimeoutSetSettings) // timeout parameter ignored for fire-and-forget
}

// GetKeymap retrieves device keymap via RPC.
func (ms *MagicStickDevice) GetKeymap(defaults bool) (*GetKeymapReply, error) {
	slog.Debug("GetKeymap()")

	if ms.rpcConnection == nil {
		return nil, errors.New("RPC connection not available")
	}
	return ms.rpcConnection.GetKeymap(defaults, RpcTimeoutGetKeymap)
}

// SetKeymap updates device keymap via RPC.
func (ms *MagicStickDevice) SetKeymap(items []string) (*SetKeymapReply, error) {
	slog.Debug("SetKeymap()")

	if ms.rpcConnection == nil {
		return nil, errors.New("RPC connection not available")
	}
	return ms.rpcConnection.SetKeymap(items, RpcTimeoutSetKeymap)
}

// SaveConfig saves current configuration to device via RPC (fire-and-forget, no response expected).
func (ms *MagicStickDevice) SaveConfig() error {
	slog.Debug("SaveConfig()")

	if ms.rpcConnection == nil {
		return errors.New("RPC connection not available")
	}
	// Send the request but don't wait for response (fire-and-forget)
	return ms.rpcConnection.SaveConfig(RpcTimeoutSaveConfig) // timeout parameter ignored for fire-and-forget
}

// startBackground launches goroutine to continuously read input reports from charger interface and invoke callback.
func (ms *MagicStickDevice) startBackground() {
	slog.Debug("startBackground() called")
	ms.backgroundOnce.Do(func() {
		slog.Debug("startBackground() executing - first time")
		ms.stopCh = make(chan struct{})

		// Device status monitor
		ms.backgroundWg.Add(1)
		go func() {
			defer ms.backgroundWg.Done()
			ticker := time.NewTicker(500 * time.Millisecond) // Check every 500ms
			defer ticker.Stop()

			for {
				select {
				case <-ms.stopCh:
					slog.Debug("device monitor goroutine: stop signal received")
					return
				case <-ticker.C:
					// Check if any interface is no longer open
					var disconnectedInterface string
					if ms.chargerInterface != nil && !ms.chargerInterface.IsOpen() {
						disconnectedInterface = "charger"
					} else if ms.cmdInterface != nil && !ms.cmdInterface.IsOpen() {
						disconnectedInterface = "command"
					} else if ms.hidioInterface != nil && !ms.hidioInterface.IsOpen() {
						disconnectedInterface = "hidio"
					}

					if disconnectedInterface != "" {
						slog.Debug("interface closed - device disconnected", "interface", disconnectedInterface)
						ms.notifyDisconnect(fmt.Errorf("%s interface closed", disconnectedInterface))
						return
					}
				}
			}
		}()

		// Check if this is a Linux or Darwin composite device (all interfaces point to same device)
		isCompositeDevice := (runtime.GOOS == "linux" || runtime.GOOS == "darwin") &&
			ms.chargerInterface != nil && ms.chargerInterface.IsOpen() &&
			ms.chargerInterface == ms.cmdInterface &&
			ms.chargerInterface == ms.hidioInterface

		// Charger input reader
		// On Linux and Darwin, all interfaces share the same device, so we need to handle battery reports differently
		slog.Debug("Checking charger interface for battery monitoring",
			"chargerInterface_nil", ms.chargerInterface == nil,
			"chargerInterface_open", ms.chargerInterface != nil && ms.chargerInterface.IsOpen(),
			"isCompositeDevice", isCompositeDevice,
			"platform", runtime.GOOS)

		if ms.chargerInterface != nil && ms.chargerInterface.IsOpen() {
			if isCompositeDevice {
				slog.Debug("Composite device detected - battery monitoring will be handled by RPC goroutine", "platform", runtime.GOOS)
				// For Linux and Darwin composite devices, battery reports will be handled by the RPC goroutine
				// since all interfaces share the same device and the RPC goroutine reads all reports
			} else {
				// For non-Linux/Darwin or separate devices, use dedicated charger goroutine
				slog.Debug("Starting dedicated charger goroutine for battery monitoring")
				ms.backgroundWg.Add(1)
				go func(dev *usbhid.Device) {
					defer ms.backgroundWg.Done()
					for {
						// Use a channel to make GetInputReport cancellable
						resultCh := make(chan struct {
							rid byte
							buf []byte
							err error
						}, 1)

						go func() {
							rid, buf, err := dev.GetInputReport()
							resultCh <- struct {
								rid byte
								buf []byte
								err error
							}{rid, buf, err}
						}()

						select {
						case <-ms.stopCh:
							slog.Debug("charger goroutine: stop signal received")
							return
						case result := <-resultCh:
							if result.err != nil {
								slog.Error("charger goroutine: GetInputReport error", "error", result.err)
								slog.Debug("charger goroutine: device disconnected, stopping")
								ms.notifyDisconnect(result.err)
								return
							}
							slog.Debug("Charger report received", "rid", fmt.Sprintf("0x%02x", result.rid), "length", len(result.buf), "data", result.buf)
							if result.rid != ReportIdCharger || len(result.buf) <= 1 {
								slog.Debug("Skipping charger report", "rid", fmt.Sprintf("0x%02x", result.rid), "expected", fmt.Sprintf("0x%02x", ReportIdCharger), "length", len(result.buf))
								continue
							}
							// Call standard callback if set
							if ms.batteryCallback != nil {
								slog.Debug("Calling battery callback from charger goroutine", "data", result.buf)
								ms.batteryCallback(result.buf)
							} else {
								slog.Debug("Battery callback is nil in charger goroutine, skipping callback")
							}
						}
					}
				}(ms.chargerInterface)
			}
		}

		// RPC event monitoring
		if ms.rpcConnection != nil {
			// Set up event callback
			ms.rpcConnection.SetEventCallback(func(name string, payload string) {
				if ms.rpcEventCallback != nil {
					ms.rpcEventCallback(name, payload)
				}
			})

			// For Linux composite devices, also set up battery callback on RPC connection
			if isCompositeDevice {
				slog.Debug("Setting battery callback on RPC connection for Linux composite device")
				ms.rpcConnection.SetBatteryCallback(ms.batteryCallback)
			} else {
				slog.Debug("Not setting battery callback on RPC connection - using dedicated charger goroutine", "isCompositeDevice", isCompositeDevice)
			}

			// Start RPC connection for event monitoring
			ms.rpcConnection.Start()
		}
		slog.Debug("startBackground() completed successfully")
	})
	slog.Debug("startBackground() finished - backgroundOnce.Do completed")
}

// stopBackground stops all background goroutines and waits for them to finish.
func (ms *MagicStickDevice) stopBackground() {
	slog.Debug("StopBackground: closing stop channel")
	if ms.stopCh != nil {
		close(ms.stopCh)
		ms.stopCh = nil
	}
	slog.Debug("StopBackground: waiting for goroutines to finish")

	// Wait for goroutines to finish with a timeout to avoid hanging on disconnect
	done := make(chan struct{})
	go func() {
		ms.backgroundWg.Wait()
		close(done)
	}()

	select {
	case <-done:
		slog.Debug("StopBackground: all goroutines finished")
	case <-time.After(2 * time.Second):
		slog.Debug("StopBackground: timeout waiting for goroutines, continuing anyway")
	}
}
