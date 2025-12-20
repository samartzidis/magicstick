package main

import (
	"log/slog"

	"magicstick-ui/hid"
	"magicstick-ui/keyboard_input"

	"github.com/wailsapp/wails/v3/pkg/application"
)

// DeviceService wraps the hid.Device to expose it as a Wails service
type DeviceService struct {
	device *hid.Device
	app    *application.App
}

// NewDeviceService creates a new DeviceService
func NewDeviceService(device *hid.Device, app *application.App) *DeviceService {
	return &DeviceService{
		device: device,
		app:    app,
	}
}

// EnumerateDevices scans the system for Magic Stick devices
func (d *DeviceService) EnumerateDevices() ([]*hid.DeviceInfo, error) {
	return d.device.EnumerateDevices()
}

// Open opens a device by serial number and sets up event callbacks
func (d *DeviceService) Open(serial string, lock bool) error {
	// Set up callbacks that emit Wails v3 events
	d.setupEventCallbacks()
	
	return d.device.Open(serial, lock)
}

// setupEventCallbacks sets up device callbacks that emit Wails v3 events
func (d *DeviceService) setupEventCallbacks() {
	// Battery callback
	batteryCallback := func(raw []byte) {
		if len(raw) >= 2 {
			slog.Debug("Battery callback received data", "data", raw)
			status := int(raw[0])
			level := int(raw[1])
			
			if d.app != nil {
				d.app.Event.Emit(EventBatteryReport, BatteryReportEvent{
					Status: status,
					Level:  level,
				})
			}
		}
	}
	d.device.SetBatteryCallback(batteryCallback)

	// Disconnect callback
	disconnectCallback := func(err error) {
		slog.Error("Disconnect callback received error", "error", err)
		if d.app != nil {
			d.app.Event.Emit(EventDeviceDisconnected, DeviceDisconnectedEvent{
				Error: err.Error(),
			})
		}
	}
	d.device.SetDisconnectCallback(disconnectCallback)

	// RPC event callback
	rpcCallback := func(name string, payload string) {
		slog.Debug("RPC callback received event", "event_name", name, "payload", payload)
		
		// Handle special events that need immediate processing
		if name == "send_unicode_char_event" {
			if err := keyboard_input.HandleSendUnicodeCharEvent(payload); err != nil {
				slog.Error("Failed to handle SendUnicodeCharEvent", "error", err)
			}
		}
		
		if d.app != nil {
			d.app.Event.Emit(EventRpc, RpcEvent{
				Name:    name,
				Payload: payload,
			})
		}
	}
	d.device.SetRpcEventCallback(rpcCallback)
}

// Close closes the currently opened device
func (d *DeviceService) Close() {
	d.device.Close()
}

// RequestBatteryReport manually sends a request for battery report
func (d *DeviceService) RequestBatteryReport() error {
	return d.device.RequestBatteryReport()
}

// GetSettings retrieves current device settings via RPC
func (d *DeviceService) GetSettings() (*hid.GetSettingsReply, error) {
	return d.device.GetSettings()
}

// SetSettings updates device settings via RPC
func (d *DeviceService) SetSettings(req *hid.SetSettingsRequest) error {
	return d.device.SetSettings(req)
}

// GetKeymap retrieves device keymap via RPC
func (d *DeviceService) GetKeymap(defaults bool) (*hid.GetKeymapReply, error) {
	return d.device.GetKeymap(defaults)
}

// SetKeymap updates device keymap via RPC
func (d *DeviceService) SetKeymap(items []string) (*hid.SetKeymapReply, error) {
	return d.device.SetKeymap(items)
}

// SaveConfig saves current configuration to device via RPC
func (d *DeviceService) SaveConfig() error {
	return d.device.SaveConfig()
}
