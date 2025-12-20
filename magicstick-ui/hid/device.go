package hid

import (
	"errors"
	"fmt"
	"log/slog"
	"runtime"
	"sort"
	"sync"
	"time"

	"magicstick-ui/usbhid"
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

	// Device identification
	VendorIdMagicStick  uint16 = 0x2e8a
	ProductIdMagicStick uint16 = 0xc010

	// Platform detection
	PlatformLinux   = "linux"
	PlatformDarwin  = "darwin"
	PlatformWindows = "windows"

	// Device interface types
	InterfaceCommand = "command"
	InterfaceCharger = "charger"
	InterfaceHidIo   = "hidio"

	// Background monitoring
	BackgroundStopTimeout = 2 * time.Second

	// Linux/Darwin specific paths
	LinuxHidRawPrefix     = "/dev/hidraw"
	DarwinIOServicePrefix = "IOService:"

	// RPC chunking
	RpcChunkSize    = 32
	RpcMaxChunkSize = 33
)

// isCompositePlatform indicates if the current platform uses composite devices (Linux/Darwin)
var isCompositePlatform = runtime.GOOS == PlatformLinux || runtime.GOOS == PlatformDarwin

// Device bundles the command and charger interfaces of one physical device
// and manages background workers for periodic requests and input report handling.
type Device struct {
	// For Windows: separate interfaces for each function
	hidCmd     *usbhid.Device
	hidCharger *usbhid.Device
	hidIo      *usbhid.Device

	// For Linux/Darwin: single composite interface
	hidComposite *usbhid.Device

	// RPC connection for event monitoring
	rpcConnection *DeviceRpc

	// HID dispatcher for centralized report handling
	dispatcher *HidDispatcher

	// Mutex to protect EnumerateDevices from parallel calls
	enumerateMutex sync.Mutex

	// callbacks
	batteryCallback    func(raw []byte)
	disconnectCallback func(error)                       // Called when device disconnects
	rpcEventCallback   func(name string, payload string) // Called when RPC events are received
	disconnectNotified bool                              // Prevents multiple disconnect notifications
}

// BatteryHandler implements ReportHandler for battery reports
type BatteryHandler struct {
	callback func(raw []byte)
}

// HandleReport processes battery reports
func (h *BatteryHandler) HandleReport(reportID byte, data []byte) {
	if reportID == ReportIdCharger && len(data) >= 2 {
		slog.Debug("Battery report received", "rid", reportID, "length", len(data))
		if h.callback != nil {
			h.callback(data)
		}
	}
}

// MagicStickInfo represents an enumerated candidate device before opening.
type DeviceInfo struct {
	Index        int
	VendorID     uint16
	ProductID    uint16
	Product      string // Device Model (from Product())
	Serial       string // Device Serial (from SerialNumber())
	Manufacturer string

	// For Windows: separate device references
	hidCmd     *usbhid.Device
	hidCharger *usbhid.Device
	hidIo      *usbhid.Device

	// For Linux/Darwin: single composite device reference
	hidComposite *usbhid.Device
}

// EnumerateDevices scans the system for Magic Stick devices and returns
// one entry per physical device that has either command or charger interface present.
// Results are sorted by serial for stable ordering.
func (ms *Device) EnumerateDevices() ([]*DeviceInfo, error) {
	slog.Debug("EnumerateDevices()")

	ms.enumerateMutex.Lock()
	devices, err := usbhid.Enumerate(func(d *usbhid.Device) bool {
		return d.VendorId() == VendorIdMagicStick && d.ProductId() == ProductIdMagicStick
	})
	ms.enumerateMutex.Unlock()

	if err != nil {
		return nil, err
	}
	slog.Debug("Found raw USB devices", "count", len(devices), "vendor", fmt.Sprintf("0x%04x", VendorIdMagicStick), "product", fmt.Sprintf("0x%04x", ProductIdMagicStick))
	if len(devices) == 0 {
		return []*DeviceInfo{}, nil
	}

	// Group devices by serial when available; otherwise group by product string
	byKey := map[string]*DeviceInfo{}
	for _, d := range devices {
		deviceInfo := createOrUpdateDeviceInfo(byKey, d)
		assignInterfaceToDevice(deviceInfo, d)
	}

	// Build and sort the final result
	result := buildAndSortDeviceList(byKey)

	slog.Debug("Returning MagicStick devices", "count", len(result))
	return result, nil
}

// Open opens the command and charger interfaces (if available). If lock is true,
// the device is exclusively locked so concurrent processes cannot access it.
// This method can be called multiple times to reopen after disconnection.
func (ms *Device) Open(serial string, lock bool) error {
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
	deviceInfo, err := ms.findDeviceBySerial(devices, serial)
	if err != nil {
		slog.Error("Open failed - device not found during re-enumeration", "serial", serial)
		return err
	}
	slog.Debug("Device found during re-enumeration", "serial", deviceInfo.Serial)

	// Prepare device for opening (cleanup existing state)
	// If already opened, close existing device first
	if isCompositePlatform {
		if ms.hidComposite != nil {
			slog.Debug("Open: closing existing composite device before opening new one")
			ms.closeInternal()
		}
	} else {
		if ms.hidCmd != nil || ms.hidCharger != nil || ms.hidIo != nil {
			slog.Debug("Open: closing existing device before opening new one")
			ms.closeInternal()
		}
	}

	// Reset disconnect notification flag for new connection
	ms.disconnectNotified = false

	// Debug: Log available interfaces
	slog.Debug("Device interfaces", "command", deviceInfo.hidCmd != nil, "charger", deviceInfo.hidCharger != nil, "hidio", deviceInfo.hidIo != nil)

	// Open device interfaces based on platform and device type
	if isCompositePlatform {
		if err := ms.openCompositeDevice(deviceInfo, lock); err != nil {
			return err
		}
	} else {
		if err := ms.openDevice(deviceInfo, lock); err != nil {
			return err
		}
	}

	// Validate open
	if isCompositePlatform {
		if ms.hidComposite == nil {
			slog.Error("Open failed - no composite interface opened")
			return errors.New("no composite interface opened")
		}
		slog.Debug("Successfully opened composite interface")
	} else {
		if ms.hidCmd == nil && ms.hidCharger == nil && ms.hidIo == nil {
			slog.Error("Open failed - no usable interfaces to open")
			return errors.New("no usable interfaces to open")
		}
		slog.Debug("Successfully opened interfaces", "command", deviceInfo.hidCmd != nil, "charger", deviceInfo.hidCharger != nil, "hidio", deviceInfo.hidIo != nil)
	}

	// Setup RPC connection
	if err := ms.setupRpcConnection(); err != nil {
		return err
	}

	// Set up event callback
	ms.rpcConnection.SetEventCallback(func(name string, payload string) {
		if ms.rpcEventCallback != nil {
			ms.rpcEventCallback(name, payload)
		}
	})

	// Setup HID dispatcher
	ms.setupDispatcher()

	slog.Debug("Open completed successfully")
	return nil
}

// Close closes any opened interfaces and stops background monitoring.
func (ms *Device) Close() {
	slog.Debug("Close()")

	// Stop dispatcher
	if ms.dispatcher != nil {
		slog.Debug("Close: stopping HID dispatcher")
		ms.dispatcher.Stop()
		ms.dispatcher = nil
	}

	ms.rpcConnection = nil

	slog.Debug("Close: closing device interfaces")
	if isCompositePlatform {
		// Close composite device
		if ms.hidComposite != nil && ms.hidComposite.IsOpen() {
			slog.Debug("Close: closing composite interface")
			_ = ms.hidComposite.Close()
		}
	} else {
		// Close individual interfaces
		if ms.hidCharger != nil && ms.hidCharger.IsOpen() {
			slog.Debug("Close: closing charger interface")
			_ = ms.hidCharger.Close()
		}
		if ms.hidCmd != nil && ms.hidCmd.IsOpen() {
			slog.Debug("Close: closing command interface")
			_ = ms.hidCmd.Close()
		}
		if ms.hidIo != nil && ms.hidIo.IsOpen() {
			slog.Debug("Close: closing hidio interface")
			_ = ms.hidIo.Close()
		}
	}
	slog.Debug("Close: all interfaces closed")
}

// SetBatteryCallback registers a callback that receives battery data from charger reports.
// This supports both standard Go callbacks and Wails events.
func (ms *Device) SetBatteryCallback(cb func(raw []byte)) {
	slog.Debug("SetBatteryCallback()")

	ms.batteryCallback = cb

	// If dispatcher is already set up, register the handler
	if ms.dispatcher != nil {
		batteryHandler := &BatteryHandler{callback: cb}
		ms.dispatcher.RegisterHandler(ReportIdCharger, batteryHandler)
	}
}

// SetDisconnectCallback registers a callback that is called when the device disconnects.
// The error parameter indicates the reason for disconnection (e.g., USB unplugged).
func (ms *Device) SetDisconnectCallback(cb func(error)) {
	slog.Debug("SetDisconnectCallback()")

	ms.disconnectCallback = cb
}

// SetRpcEventCallback registers a callback that receives RPC events from the device.
// The callback receives the event name and the full JSON payload.
func (ms *Device) SetRpcEventCallback(cb func(name string, payload string)) {
	slog.Debug("SetRpcEventCallback()")

	ms.rpcEventCallback = cb
}

// RequestBatteryReport manually sends a request for battery report via command interface.
// The report will arrive via the battery callback when received.
func (ms *Device) RequestBatteryReport() error {
	slog.Debug("RequestBatteryReport()")

	var cmdDev *usbhid.Device
	if isCompositePlatform {
		cmdDev = ms.hidComposite
	} else {
		cmdDev = ms.hidCmd
	}

	if cmdDev == nil || !cmdDev.IsOpen() {
		return errors.New("command interface not available")
	}
	payload := []byte{ReportIdCharger, 0, 0, 0, 0, 0, 0, 0}
	outLen := int(cmdDev.GetOutputReportLength())
	if outLen <= 0 {
		return errors.New("invalid output report length")
	}
	buf := make([]byte, outLen)
	copy(buf, payload)
	return cmdDev.SetOutputReport(ReportRequestById, buf)
}

// GetSettings retrieves current device settings via RPC.
func (ms *Device) GetSettings() (*GetSettingsReply, error) {
	slog.Debug("GetSettings()")

	if ms.rpcConnection == nil {
		return nil, errors.New("RPC connection not available")
	}
	return ms.rpcConnection.GetSettings(RpcTimeoutGetSettings)
}

// SetSettings updates device settings via RPC (fire-and-forget, no response expected).
func (ms *Device) SetSettings(req *SetSettingsRequest) error {
	slog.Debug("SetSettings()")

	if ms.rpcConnection == nil {
		return errors.New("RPC connection not available")
	}
	// Send the request but don't wait for response (fire-and-forget)
	return ms.rpcConnection.SetSettings(*req, RpcTimeoutSetSettings) // timeout parameter ignored for fire-and-forget
}

// GetKeymap retrieves device keymap via RPC.
func (ms *Device) GetKeymap(defaults bool) (*GetKeymapReply, error) {
	slog.Debug("GetKeymap()")

	if ms.rpcConnection == nil {
		return nil, errors.New("RPC connection not available")
	}
	return ms.rpcConnection.GetKeymap(defaults, RpcTimeoutGetKeymap)
}

// SetKeymap updates device keymap via RPC.
func (ms *Device) SetKeymap(items []string) (*SetKeymapReply, error) {
	slog.Debug("SetKeymap()")

	if ms.rpcConnection == nil {
		return nil, errors.New("RPC connection not available")
	}
	return ms.rpcConnection.SetKeymap(items, RpcTimeoutSetKeymap)
}

// SaveConfig saves current configuration to device via RPC (fire-and-forget, no response expected).
func (ms *Device) SaveConfig() error {
	slog.Debug("SaveConfig()")

	if ms.rpcConnection == nil {
		return errors.New("RPC connection not available")
	}
	// Send the request but don't wait for response (fire-and-forget)
	return ms.rpcConnection.SaveConfig(RpcTimeoutSaveConfig) // timeout parameter ignored for fire-and-forget
}

// generateDeviceKey creates a unique key for grouping devices by serial or product info.
func generateDeviceKey(d *usbhid.Device) string {
	key := d.SerialNumber()
	if key == "" {
		key = fmt.Sprintf("%s|%04x:%04x", d.Product(), d.VendorId(), d.ProductId())
	}
	return key
}

// createOrUpdateDeviceInfo creates a new DeviceInfo or updates an existing one with device details.
func createOrUpdateDeviceInfo(byKey map[string]*DeviceInfo, d *usbhid.Device) *DeviceInfo {
	key := generateDeviceKey(d)
	deviceInfo := byKey[key]

	if deviceInfo == nil {
		deviceInfo = &DeviceInfo{
			VendorID:     d.VendorId(),
			ProductID:    d.ProductId(),
			Product:      d.Product(),
			Serial:       d.SerialNumber(),
			Manufacturer: d.Manufacturer(),
		}
		byKey[key] = deviceInfo
	}

	// Update descriptive fields if missing and available from this interface
	if deviceInfo.Product == "" {
		deviceInfo.Product = d.Product()
	}
	if deviceInfo.Serial == "" {
		deviceInfo.Serial = d.SerialNumber()
	}
	if deviceInfo.Manufacturer == "" {
		deviceInfo.Manufacturer = d.Manufacturer()
	}

	return deviceInfo
}

// assignInterfaceToDevice assigns the appropriate interface to the device based on usage.
func assignInterfaceToDevice(deviceInfo *DeviceInfo, d *usbhid.Device) {
	usage := d.Usage()
	devicePath := d.Path()

	slog.Debug("Device interface", "serial", d.SerialNumber(), "usage", fmt.Sprintf("0x%04x", usage), "path", devicePath)

	if isCompositePlatform {
		// For Linux/Darwin composite devices, use single device reference
		if deviceInfo.hidComposite == nil {
			slog.Debug("Assigning composite device", "serial", d.SerialNumber(), "platform", runtime.GOOS)
			deviceInfo.hidComposite = d
		}
		return
	}

	// For Windows: assign to specific interface based on usage
	switch usage {
	case UsageRequestReportById:
		slog.Debug("Found Command interface", "serial", d.SerialNumber())
		deviceInfo.hidCmd = d
	case UsageCharger:
		slog.Debug("Found Charger interface", "serial", d.SerialNumber())
		deviceInfo.hidCharger = d
	case UsageHidIo:
		slog.Debug("Found HidIo interface", "serial", d.SerialNumber())
		deviceInfo.hidIo = d
	default:
		slog.Debug("Unknown usage", "usage", fmt.Sprintf("0x%04x", usage), "serial", d.SerialNumber())
	}
}

// buildAndSortDeviceList converts the device map to a sorted slice with indices.
func buildAndSortDeviceList(byKey map[string]*DeviceInfo) []*DeviceInfo {
	// Build slice
	result := make([]*DeviceInfo, 0, len(byKey))
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

		// Log device summary with interface availability
		if isCompositePlatform {
			slog.Debug("Composite device summary", "index", idx, "serial", v.Serial, "platform", runtime.GOOS)
			slog.Debug("Device interface availability", "index", idx, "serial", v.Serial, "hidComposite", v.hidComposite != nil, "isComposite", isCompositePlatform)
		} else {
			slog.Debug("Device interface availability", "index", idx, "serial", v.Serial, "command", v.hidCmd != nil, "charger", v.hidCharger != nil, "hidio", v.hidIo != nil, "isComposite", isCompositePlatform)
		}
	}

	return result
}

// findDeviceBySerial searches for a device with the given serial number in the enumerated devices list.
func (ms *Device) findDeviceBySerial(devices []*DeviceInfo, serial string) (*DeviceInfo, error) {
	for _, d := range devices {
		if d.Serial == serial {
			return d, nil
		}
	}
	return nil, fmt.Errorf("device with serial %s not found during re-enumeration", serial)
}

// openCompositeDevice opens a Linux/Darwin composite device where all interfaces share the same device.
func (ms *Device) openCompositeDevice(deviceInfo *DeviceInfo, lock bool) error {
	slog.Debug("Composite device - opening single device for all interfaces", "platform", runtime.GOOS)

	if deviceInfo.hidComposite == nil {
		return errors.New("no composite device available")
	}

	slog.Debug("Opening composite device")
	if err := deviceInfo.hidComposite.Open(lock); err != nil {
		slog.Error("Open failed - composite device open failed", "error", err)
		return fmt.Errorf("open composite device failed: %w", err)
	}
	slog.Debug("Composite device opened successfully")

	// Use the single device reference for all interfaces
	ms.hidComposite = deviceInfo.hidComposite
	slog.Debug("Composite device assigned to hidComposite")

	return nil
}

// openDevice opens each interface individually for non-composite devices.
func (ms *Device) openDevice(deviceInfo *DeviceInfo, lock bool) error {
	// Open command first if present
	if deviceInfo.hidCmd != nil {
		slog.Debug("Opening Command interface")
		if err := deviceInfo.hidCmd.Open(lock); err != nil {
			slog.Error("Open failed - command interface open failed", "error", err)
			return fmt.Errorf("open command interface failed: %w", err)
		}
		ms.hidCmd = deviceInfo.hidCmd
		slog.Debug("Command interface opened successfully")
	} else {
		slog.Debug("No Command interface to open")
	}

	// Open charger if present
	if deviceInfo.hidCharger != nil {
		slog.Debug("Opening Charger interface")
		if err := deviceInfo.hidCharger.Open(lock); err != nil {
			slog.Error("Open failed - charger interface open failed", "error", err)
			// best effort: close cmd if we opened it
			if ms.hidCmd != nil {
				_ = ms.hidCmd.Close()
				ms.hidCmd = nil
			}
			return fmt.Errorf("open charger interface failed: %w", err)
		}
		ms.hidCharger = deviceInfo.hidCharger
		slog.Debug("Charger interface opened successfully")
	} else {
		slog.Debug("No Charger interface to open")
	}

	// Open hidio if present
	if deviceInfo.hidIo != nil {
		slog.Debug("Opening HidIo interface")
		if err := deviceInfo.hidIo.Open(lock); err != nil {
			slog.Error("Open failed - hidio interface open failed", "error", err)
			// best effort: close previously opened
			if ms.hidCmd != nil {
				_ = ms.hidCmd.Close()
				ms.hidCmd = nil
			}
			if ms.hidCharger != nil {
				_ = ms.hidCharger.Close()
				ms.hidCharger = nil
			}
			return fmt.Errorf("open hidio interface failed: %w", err)
		}
		ms.hidIo = deviceInfo.hidIo
		slog.Debug("HidIo interface opened successfully")
	} else {
		slog.Debug("No HidIo interface to open")
	}

	return nil
}

// setupRpcConnection creates and configures the RPC connection if HidIo interface is available.
func (ms *Device) setupRpcConnection() error {
	var hidIo *usbhid.Device
	if isCompositePlatform {
		hidIo = ms.hidComposite
	} else {
		hidIo = ms.hidIo
	}

	if hidIo != nil {
		slog.Debug("Creating RPC connection")
		rpc, err := NewDeviceRpc(hidIo)
		if err != nil {
			slog.Error("Open failed - create RPC connection failed", "error", err)
			return fmt.Errorf("create RPC connection failed: %w", err)
		}
		ms.rpcConnection = rpc
		slog.Debug("RPC connection created successfully")
	} else {
		slog.Debug("No HidIo interface available for RPC connection")
	}
	return nil
}

// setupDispatcher initializes the HID dispatcher and registers devices and handlers
func (ms *Device) setupDispatcher() {
	slog.Debug("Setting up HID dispatcher")

	// Create dispatcher
	ms.dispatcher = NewHidDispatcher()

	// Add devices to dispatcher (only those that provide input reports)
	if isCompositePlatform {
		if ms.hidComposite != nil {
			ms.dispatcher.AddDevice(ms.hidComposite)
		}
	} else {
		// Only add charger and hidio interfaces - they provide input reports
		// Command interface is for sending commands, not receiving reports
		if ms.hidCharger != nil {
			ms.dispatcher.AddDevice(ms.hidCharger)
		}
		if ms.hidIo != nil {
			ms.dispatcher.AddDevice(ms.hidIo)
		}
	}

	// Register battery handler
	if ms.batteryCallback != nil {
		batteryHandler := &BatteryHandler{callback: ms.batteryCallback}
		ms.dispatcher.RegisterHandler(ReportIdCharger, batteryHandler)
	}

	// Register RPC handler if available
	if ms.rpcConnection != nil {
		ms.dispatcher.RegisterHandler(ReportIdHidIo, ms.rpcConnection)
	}

	// Set up read error callback
	ms.dispatcher.SetReadErrorCallback(func(deviceIndex int, err error) {
		slog.Debug("HidDispatcher detected read error", "deviceIndex", deviceIndex, "error", err)

		if ms.disconnectCallback != nil && !ms.disconnectNotified {
			ms.disconnectNotified = true
			ms.disconnectCallback(err)
		}

		// Call closeInternal asynchronously to avoid deadlock
		go ms.closeInternal()
	})

	// Start dispatcher
	ms.dispatcher.Start()

	slog.Debug("HID dispatcher setup completed")
}

func (ms *Device) closeInternal() {

	// Stop dispatcher
	if ms.dispatcher != nil {
		ms.dispatcher.Stop()
	}

	ms.dispatcher = nil
	ms.rpcConnection = nil

	if isCompositePlatform {
		// Close composite device
		if ms.hidComposite != nil && ms.hidComposite.IsOpen() {
			_ = ms.hidComposite.Close()
		}

		ms.hidComposite = nil
	} else {
		// Close individual interfaces
		if ms.hidCharger != nil && ms.hidCharger.IsOpen() {
			_ = ms.hidCharger.Close()
		}
		if ms.hidCmd != nil && ms.hidCmd.IsOpen() {
			_ = ms.hidCmd.Close()
		}
		if ms.hidIo != nil && ms.hidIo.IsOpen() {
			_ = ms.hidIo.Close()
		}

		// Clear interface references
		ms.hidCmd = nil
		ms.hidCharger = nil
		ms.hidIo = nil
	}
}
