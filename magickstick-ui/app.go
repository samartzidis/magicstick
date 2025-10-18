package main

import (
	"bytes"
	"context"
	"embed"
	"fmt"
	"image"
	"image/color"
	"image/png"
	"log/slog"
	"os"
	"path/filepath"
	"runtime"
	"strings"

	"magicstick-ui/hid"
	"magicstick-ui/keyboard_input"
	"magicstick-ui/systray"

	ico "github.com/Kodeworks/golang-image-ico"
	"github.com/nfnt/resize"

	wailsRuntime "github.com/wailsapp/wails/v2/pkg/runtime"
)

const (
	BatteryStatusGood        = 0x01 // present/OK
	BatteryStatusCharging    = 0x02 // charging
	BatteryStatusDischarging = 0x04 // discharging
)

//go:embed assets/icons/Battery.png assets/icons/Battery_dark.png assets/icons/Indicator_*.png assets/icons/Missing.png assets/icons/Missing_dark.png assets/icons/Charging.png assets/icons/Charging_dark.png assets/icons/Charged.png assets/icons/Charged_dark.png
var batteryIcons embed.FS

// Global Device instance for callback application
var globalMagicStickDevice *hid.Device

// App struct
type App struct {
	ctx                context.Context
	batteryCallback    func([]byte)
	disconnectCallback func(error)
	rpcCallback        func(string, string)
	// tray-related state (managed by frontend)
	currentDevice *hid.Device
}

// NewApp creates a new App application struct
func NewApp() *App {
	return &App{}
}

// SetBatteryCallback creates and applies a callback function that emits Wails events
func (a *App) SetBatteryCallback() {
	slog.Debug("SetBatteryCallback called - creating callback that emits Wails events")

	// Create the callback function
	batteryCallback := func(raw []byte) {
		if len(raw) >= 2 {
			slog.Debug("Battery callback received data", "data", raw)

			// Extract battery data
			status := int(raw[0])
			level := int(raw[1])

			// Emit Wails event for frontend - frontend decides how to handle tray updates
			wailsRuntime.EventsEmit(a.ctx, "battery-report", map[string]interface{}{
				"status": status,
				"level":  level,
			})
		} else {
			slog.Debug("Battery callback received invalid data", "data", raw)
		}
	}

	// Apply the callback to the global MagicStickDevice if it exists
	if globalMagicStickDevice != nil {
		slog.Debug("Applying battery callback to global MagicStickDevice", "address", fmt.Sprintf("%p", globalMagicStickDevice))
		globalMagicStickDevice.SetBatteryCallback(batteryCallback)
		slog.Debug("Battery callback applied successfully")
	} else {
		slog.Debug("Global MagicStickDevice is nil, storing callback for later")
		a.batteryCallback = batteryCallback
	}
}

// SetDisconnectCallback creates and applies a callback function that emits Wails events
func (a *App) SetDisconnectCallback() {
	slog.Debug("SetDisconnectCallback called - creating callback that emits Wails events")

	// Create the callback function
	disconnectCallback := func(err error) {
		slog.Error("Disconnect callback received error", "error", err)

		// Emit Wails event for frontend - frontend decides how to handle tray updates
		wailsRuntime.EventsEmit(a.ctx, "device-disconnected", map[string]interface{}{
			"error": err.Error(),
		})
	}

	// Apply the callback to the global MagicStickDevice if it exists
	if globalMagicStickDevice != nil {
		slog.Debug("Applying disconnect callback to global MagicStickDevice")
		globalMagicStickDevice.SetDisconnectCallback(disconnectCallback)
	} else {
		slog.Debug("Global MagicStickDevice is nil, storing callback for later")
		a.disconnectCallback = disconnectCallback
	}
}

// SetRpcEventCallback creates and applies a callback function that emits Wails events
func (a *App) SetRpcEventCallback() {
	slog.Debug("SetRpcEventCallback called - creating callback that emits Wails events")

	// Create the callback function
	rpcCallback := func(name string, payload string) {
		slog.Debug("RPC callback received event",
			"event_name", name,
			"payload", payload,
		)

		// Handle special events that need immediate processing
		if name == "send_unicode_char_event" {
			if err := keyboard_input.HandleSendUnicodeCharEvent(payload); err != nil {
				slog.Error("Failed to handle SendUnicodeCharEvent", "error", err)
			}
		}

		wailsRuntime.EventsEmit(a.ctx, "rpc-event", map[string]interface{}{
			"name":    name,
			"payload": payload,
		})
	}

	// Apply the callback to the global MagicStickDevice if it exists
	if globalMagicStickDevice != nil {
		slog.Debug("Applying RPC callback to global MagicStickDevice")
		globalMagicStickDevice.SetRpcEventCallback(rpcCallback)
	} else {
		slog.Debug("Global MagicStickDevice is nil, storing callback for later")
		a.rpcCallback = rpcCallback
	}
}

// SetGlobalMagicStickDevice sets the global MagicStickDevice instance
func (a *App) SetGlobalMagicStickDevice() {
	slog.Debug("SetGlobalMagicStickDevice called - setting global instance")
	// Use the global instance from main.go
	globalMagicStickDevice = globalMagicStickDeviceInstance
	slog.Debug("Global MagicStickDevice set", "address", fmt.Sprintf("%p", globalMagicStickDevice))

	// Apply any stored callbacks to the device (and ensure battery callback exists)
	if a.batteryCallback != nil {
		slog.Debug("Applying stored battery callback to global MagicStickDevice")
		globalMagicStickDevice.SetBatteryCallback(a.batteryCallback)
		a.batteryCallback = nil // Clear stored callback
	} else {
		slog.Debug("No stored battery callback to apply")
		// Create and apply a fresh battery callback to avoid race conditions
		a.SetBatteryCallback()
	}

	if a.disconnectCallback != nil {
		slog.Debug("Applying stored disconnect callback to global MagicStickDevice")
		globalMagicStickDevice.SetDisconnectCallback(a.disconnectCallback)
		a.disconnectCallback = nil // Clear stored callback
	} else {
		slog.Debug("No stored disconnect callback to apply")
	}

	if a.rpcCallback != nil {
		slog.Debug("Applying stored RPC callback to global MagicStickDevice")
		globalMagicStickDevice.SetRpcEventCallback(a.rpcCallback)
		a.rpcCallback = nil // Clear stored callback
	} else {
		slog.Debug("No stored RPC callback to apply")
	}
}

// startup is called when the app starts. The context is saved
// so we can call the runtime methods
func (a *App) startup(ctx context.Context) {
	a.ctx = ctx

	// Set the Wails context for logging
	SetWailsLoggingContext(ctx)

	// Setup system tray
	a.setupSystemTray()
}

// DomReady is called after the front-end dom has been loaded
func (a *App) DomReady(ctx context.Context) {
	a.ctx = ctx
}

// BeforeClose is called when the application is about to quit,
// either by clicking the window close button or calling runtime.Quit
func (a *App) BeforeClose(ctx context.Context) (prevent bool) {
	// Always allow the app to close completely
	slog.Info("Application closing")

	// Ensure the system tray is cleanly shut down so the icon disappears immediately
	// It's safe to call even if systray isn't running
	systray.Quit()

	return false // Allow the close
}

// SetTrayIcon sets the tray icon with the provided icon data
// This is called by the frontend with pre-processed icon data
func (a *App) SetTrayIcon(iconData []byte) {
	slog.Debug("SetTrayIcon called", "bytes", len(iconData))

	// Update the tray icon
	defer func() {
		if r := recover(); r != nil {
			slog.Error("Failed to set tray icon", "error", r)
		}
	}()

	if len(iconData) > 0 {
		systray.SetIcon(iconData)
		slog.Debug("Tray icon updated successfully")
	}
}

// SetTrayTooltip sets the tray tooltip text
func (a *App) SetTrayTooltip(tooltip string) {
	defer func() {
		if r := recover(); r != nil {
			slog.Error("Failed to set tooltip", "error", r)
		}
	}()

	systray.SetTooltip(tooltip)
	slog.Debug("Tray tooltip set", "tooltip", strings.ReplaceAll(tooltip, "%", "%%"))
}

// LoadIconData loads icon data from the embedded filesystem
// Returns the raw icon data that can be used for blending
func (a *App) LoadIconData(iconPath string) ([]byte, error) {
	slog.Debug("Loading icon", "path", iconPath)

	iconData, err := batteryIcons.ReadFile(iconPath)
	if err != nil {
		return nil, fmt.Errorf("failed to read icon %s: %v", iconPath, err)
	}

	return iconData, nil
}

// BlendIconData blends multiple icon data arrays and returns the result
// Takes icon paths and returns blended icon data
func (a *App) BlendIconData(iconPaths []string) ([]byte, error) {
	if len(iconPaths) == 0 {
		return nil, fmt.Errorf("at least one icon path is required")
	}

	slog.Debug("Blending icons", "count", len(iconPaths), "paths", iconPaths)

	// Load and decode all icons
	var imagesToBlend []image.Image
	for _, iconPath := range iconPaths {
		iconData, err := a.LoadIconData(iconPath)
		if err != nil {
			return nil, fmt.Errorf("failed to load icon %s: %v", iconPath, err)
		}

		img, err := png.Decode(bytes.NewReader(iconData))
		if err != nil {
			return nil, fmt.Errorf("failed to decode icon %s: %v", iconPath, err)
		}

		imagesToBlend = append(imagesToBlend, img)
	}

	// Blend images
	blendedImg := a.blendImages(imagesToBlend...)

	// Convert to PNG format for all platforms (CreateIconFromResourceEx handles PNG)
	return a.convertToPNG(blendedImg)
}

// GetAvailableIcons returns a list of available icon paths from the embedded filesystem
func (a *App) GetAvailableIcons() ([]string, error) {
	entries, err := batteryIcons.ReadDir("assets/icons")
	if err != nil {
		return nil, fmt.Errorf("failed to read icons directory: %v", err)
	}

	var icons []string
	for _, entry := range entries {
		if !entry.IsDir() {
			icons = append(icons, "assets/icons/"+entry.Name())
		}
	}

	return icons, nil
}

// setupSystemTray creates and configures the system tray icon
func (a *App) setupSystemTray() {
	// Set the callback for tray icon clicks BEFORE starting the tray
	// This ensures the callback is available immediately when the tray icon is created
	slog.Debug("Setting tray icon click handler before tray startup")
	systray.SetOnClick(func() {
		slog.Debug("Tray icon click handler called - showing window")
		wailsRuntime.WindowShow(a.ctx)
		slog.Debug("Window shown from tray icon click")
	})
	slog.Debug("Tray icon click handler set successfully")

	// Start the system tray in a goroutine
	go func() {
		systray.Run(a.onTrayReady(), a.onTrayExit)
	}()
}

// onTrayReady is called when the system tray is ready
func (a *App) onTrayReady() func() {
	return func() {
		// Set title
		systray.SetTitle("magicstick-ui")

		// Note: onClick callback is already set in setupSystemTray() before tray startup
		slog.Debug("Tray ready - onClick callback already set")

		// Add "Show Window" menu item
		showItem := systray.AddMenuItem("Show Window", "Show the application window")
		go func() {
			for range showItem.ClickedCh {
				wailsRuntime.WindowShow(a.ctx)
				slog.Debug("Window shown from tray")
			}
		}()

		// Add "Quit" menu item
		quitItem := systray.AddMenuItem("Quit", "Exit the application")
		go func() {
			for range quitItem.ClickedCh {
				slog.Info("Application quitting from tray")

				// Clean up the single device instance
				if a.currentDevice != nil {
					a.currentDevice.Close()
				}

				// Quit the application
				wailsRuntime.Quit(a.ctx)
			}
		}()
	}
}

// onTrayExit is called when the system tray exits
func (a *App) onTrayExit() {
	slog.Info("System tray exited")
}

// convertToICO converts an image to ICO format for Windows system tray with high-quality rendering
func (a *App) convertToICO(img image.Image) ([]byte, error) {
	// Use high-quality resizing with Lanczos interpolation (similar to C# HighQualityBicubic)
	// Resize to 32x32 for optimal Windows system tray display
	resized := resize.Resize(32, 32, img, resize.Lanczos3)

	var buf bytes.Buffer
	err := ico.Encode(&buf, resized)
	if err != nil {
		return nil, fmt.Errorf("failed to encode ICO: %v", err)
	}
	return buf.Bytes(), nil
}

// convertToPNG converts an image to PNG format
func (a *App) convertToPNG(img image.Image) ([]byte, error) {
	var buf bytes.Buffer
	err := png.Encode(&buf, img)
	if err != nil {
		return nil, fmt.Errorf("failed to encode PNG: %v", err)
	}
	return buf.Bytes(), nil
}

// blendImages dynamically blends multiple images together, similar to C# MixBitmap
func (a *App) blendImages(images ...image.Image) image.Image {
	if len(images) == 0 {
		return nil
	}

	// Use the first image as the base
	base := images[0]
	bounds := base.Bounds()
	blended := image.NewRGBA(bounds)

	// Draw the base image first
	for y := bounds.Min.Y; y < bounds.Max.Y; y++ {
		for x := bounds.Min.X; x < bounds.Max.X; x++ {
			blended.Set(x, y, base.At(x, y))
		}
	}

	// Draw subsequent images centered on top (like C# MixBitmap)
	for i := 1; i < len(images); i++ {
		overlay := images[i]
		if overlay == nil {
			continue
		}

		overlayBounds := overlay.Bounds()

		// Calculate centered position for overlay
		overlayWidth := overlayBounds.Dx()
		overlayHeight := overlayBounds.Dy()
		baseWidth := bounds.Dx()
		baseHeight := bounds.Dy()

		// Center the overlay
		startX := bounds.Min.X + (baseWidth-overlayWidth)/2
		startY := bounds.Min.Y + (baseHeight-overlayHeight)/2

		// Blend overlay onto the base
		for y := bounds.Min.Y; y < bounds.Max.Y; y++ {
			for x := bounds.Min.X; x < bounds.Max.X; x++ {
				// Calculate overlay coordinates
				overlayX := x - startX
				overlayY := y - startY

				// Check if we're within overlay bounds
				if overlayX >= 0 && overlayX < overlayWidth &&
					overlayY >= 0 && overlayY < overlayHeight {

					// Get current blended pixel
					currentColor := blended.At(x, y)
					currentR, currentG, currentB, currentA := currentColor.RGBA()

					// Get overlay pixel
					overlayColor := overlay.At(overlayBounds.Min.X+overlayX, overlayBounds.Min.Y+overlayY)
					overlayR, overlayG, overlayB, overlayA := overlayColor.RGBA()

					// Alpha blend overlay onto current
					alpha := float64(overlayA) / 65535.0

					blendedR := uint8((float64(currentR)*(1-alpha) + float64(overlayR)*alpha) / 256)
					blendedG := uint8((float64(currentG)*(1-alpha) + float64(overlayG)*alpha) / 256)
					blendedB := uint8((float64(currentB)*(1-alpha) + float64(overlayB)*alpha) / 256)
					blendedA := uint8((float64(currentA)*(1-alpha) + float64(overlayA)*alpha) / 256)

					blended.Set(x, y, color.RGBA{blendedR, blendedG, blendedB, blendedA})
				}
			}
		}
	}

	return blended
}

// SemVerInfo contains semantic version information
type SemVerInfo struct {
	Version        string `json:"version"`
	GitCommit      string `json:"gitCommit"`
	GitCommitCount string `json:"gitCommitCount"`
}

// GetSemVer returns the semantic version information
func (a *App) GetSemVer() SemVerInfo {
	return SemVerInfo{
		Version:        Version,
		GitCommit:      GitCommit,
		GitCommitCount: GitCommitCount,
	}
}

// SetAutostart enables or disables autostart for the application
func (a *App) SetAutostart(enabled bool) error {
	if runtime.GOOS != "windows" {
		return fmt.Errorf("autostart is only supported on Windows")
	}

	// Get the executable path
	exePath, err := os.Executable()
	if err != nil {
		return fmt.Errorf("failed to get executable path: %v", err)
	}

	// Convert to absolute path
	exePath, err = filepath.Abs(exePath)
	if err != nil {
		return fmt.Errorf("failed to get absolute path: %v", err)
	}

	if enabled {
		return enableAutostart()
	} else {
		return disableAutostart()
	}
}

// GetAutostartStatus returns whether autostart is currently enabled
func (a *App) GetAutostartStatus() (bool, error) {
	if runtime.GOOS != "windows" {
		return false, fmt.Errorf("autostart is only supported on Windows")
	}

	return isAutostartEnabled()
}

// IsDarkMode returns true if Windows uses dark mode theme, true for any other OS
func (a *App) IsDarkMode() bool {
	return isDarkMode()
}
