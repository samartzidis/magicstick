package main

import (
	"embed"
	"log"
	"log/slog"
	"runtime"

	"magicstick-ui/hid"

	"github.com/wailsapp/wails/v3/pkg/application"
	"github.com/wailsapp/wails/v3/pkg/events"
	"github.com/wailsapp/wails/v3/pkg/icons"
	wailslog "github.com/wailsapp/wails/v3/pkg/services/log"
)

// Wails uses Go's `embed` package to embed the frontend files into the binary.
// Any files in the frontend/dist folder will be embedded into the binary and
// made available to the frontend.
// See https://pkg.go.dev/embed for more information.

//go:embed all:frontend/dist
var assets embed.FS

//go:embed frontend/src/assets/images/appicon.png
var appIconData []byte

// Global Device instance
var globalMagicStickDevice = &hid.Device{}

// Event name constants
const (
	EventBatteryReport      = "battery-report"
	EventDeviceDisconnected = "device-disconnected"
	EventRpc                = "rpc-event"
)

// Typed event structures for Wails v3
type BatteryReportEvent struct {
	Status int `json:"status"` // Battery status (raw value from device)
	Level  int `json:"level"`  // Battery level (0-100)
}

type DeviceDisconnectedEvent struct {
	Error string `json:"error"` // Error message describing the disconnection
}

type RpcEvent struct {
	Name    string `json:"name"`    // RPC event name (e.g., "connected", "disconnected")
	Payload string `json:"payload"` // RPC event payload (JSON string)
}

func init() {
	// Register typed events for frontend
	application.RegisterEvent[BatteryReportEvent](EventBatteryReport)
	application.RegisterEvent[DeviceDisconnectedEvent](EventDeviceDisconnected)
	application.RegisterEvent[RpcEvent](EventRpc)
}

// main function serves as the application's entry point. It initializes the application, creates a window,
// and starts a goroutine that emits a time-based event every second. It subsequently runs the application and
// logs any error that might occur.
func main() {
	// Create services with nil app initially - will be set after app creation
	appService := NewAppService(nil)
	deviceService := NewDeviceService(globalMagicStickDevice, nil)

	// Create log service for frontend logging (with Debug level enabled)
	logService := wailslog.NewWithConfig(&wailslog.Config{
		LogLevel: slog.LevelDebug,
	})

	// Create a new Wails application by providing the necessary options.
	// Variables 'Name' and 'Description' are for application metadata.
	// 'Assets' configures the asset server with the 'FS' variable pointing to the frontend files.
	// 'Bind' is a list of Go struct instances. The frontend has access to the methods of these instances.
	// 'Mac' options tailor the application when running an macOS.
	app := application.New(application.Options{
		Name:        "magicstick-ui",
		Description: "MagicStick device management utility",
		SingleInstance: &application.SingleInstanceOptions{
			UniqueID: "00099248-875b-4574-bb66-9cdaaf82ded9",
		},
		Services: []application.Service{
			application.NewService(appService),
			application.NewService(deviceService),
			application.NewService(logService),
		},
		Assets: application.AssetOptions{
			Handler: application.AssetFileServerFS(assets),
		},
		Mac: application.MacOptions{
			ActivationPolicy: application.ActivationPolicyAccessory, // Don't show in dock, use system tray
		},
	})

	// Set the app reference in services after app is created
	appService.app = app
	deviceService.app = app

	// Create a new window with the necessary options.
	// The window will be hidden on taskbar and shown/hidden via system tray.
	window := app.Window.NewWithOptions(application.WebviewWindowOptions{
		Title:            "magicstick",
		Width:            800,
		MinWidth:         800,
		Height:           600,
		MinHeight:        600,
		Frameless:        false,
		AlwaysOnTop:      true,
		Hidden:           true, // Start hidden, show via tray
		BackgroundColour: application.NewRGB(27, 38, 54),
		URL:              "/",
		Mac: application.MacWindow{
			InvisibleTitleBarHeight: 50,
			Backdrop:                application.MacBackdropTranslucent,
			TitleBar:                application.MacTitleBarHiddenInset,
		},
		Windows: application.WindowsWindow{
			HiddenOnTaskbar: true, // Hide from taskbar, show only in system tray
		},
	})

	// Create system tray
	systemTray := app.SystemTray.New()

	// Set system tray reference in app service
	appService.SetSystemTray(systemTray)

	// Set tray icon based on platform
	if runtime.GOOS == "darwin" {
		// macOS: Use template icon (automatically adapts to light/dark mode)
		// Use raw PNG bytes directly
		if len(appIconData) > 0 {
			systemTray.SetTemplateIcon(appIconData)
		} else {
			// Fallback to built-in template icon
			systemTray.SetTemplateIcon(icons.SystrayMacTemplate)
		}
	} else {
		// Windows/Linux: Use light/dark mode icons
		// Use raw PNG bytes directly for both modes
		if len(appIconData) > 0 {
			systemTray.SetIcon(appIconData)
			systemTray.SetDarkModeIcon(appIconData) // Use same icon for both modes
		} else {
			// Fallback to built-in icons
			systemTray.SetIcon(icons.SystrayLight)
			systemTray.SetDarkModeIcon(icons.SystrayDark)
		}
	}

	// Create tray menu
	trayMenu := app.Menu.New()
	trayMenu.Add("Show Window").OnClick(func(_ *application.Context) {
		window.Show()
		window.Focus()
	})
	trayMenu.Add("Hide Window").OnClick(func(_ *application.Context) {
		window.Hide()
	})
	trayMenu.AddSeparator()
	trayMenu.Add("Quit").OnClick(func(_ *application.Context) {
		app.Quit()
	})

	systemTray.SetMenu(trayMenu)
	systemTray.SetTooltip("magicstick")

	// Handle left-click on tray icon to toggle window visibility
	systemTray.OnClick(func() {
		if window.IsVisible() {
			window.Hide()
		} else {
			window.Show()
			window.Focus()
		}
	})

	// Register a hook to intercept window close events and hide instead of closing
	window.RegisterHook(events.Common.WindowClosing, func(e *application.WindowEvent) {
		window.Hide() // Hide window instead of closing
		e.Cancel()    // Cancel the default close behavior to prevent application exit
	})

	// Run the application. This blocks until the application has been exited.
	err := app.Run()

	// If an error occurred while running the application, log it and exit.
	if err != nil {
		log.Fatal(err)
	}
}
