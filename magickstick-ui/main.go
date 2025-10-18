package main

import (
	"embed"
	"flag"
	"fmt"
	"os"
	"runtime"

	"magicstick-ui/hid"

	"github.com/wailsapp/wails/v2"
	"github.com/wailsapp/wails/v2/pkg/options"
	"github.com/wailsapp/wails/v2/pkg/options/assetserver"
	"github.com/wailsapp/wails/v2/pkg/options/mac"
)

//go:embed all:frontend/dist
var assets embed.FS

// Create a single global instance of Device
var globalMagicStickDeviceInstance = &hid.Device{}

func main() {
	// Parse command line flags
	flag.Parse()

	// Create an instance of the app structure
	app := NewApp()

	// Check if running on Windows
	isWindows := runtime.GOOS == "windows"

	// Create application with options
	err := wails.Run(&options.App{
		Title:             "magicstick",
		Width:             1024,
		Height:            768,
		MinWidth:          1024,
		MinHeight:         768,
		DisableResize:     true,
		StartHidden:       isWindows,
		HideWindowOnClose: isWindows,
		AssetServer: &assetserver.Options{
			Assets: assets,
		},
		BackgroundColour: &options.RGBA{R: 27, G: 38, B: 54, A: 1},
		OnStartup:        app.startup,
		OnDomReady:       app.DomReady,
		OnBeforeClose:    app.BeforeClose,
		Bind: []interface{}{
			app,
			globalMagicStickDeviceInstance, // Use the single global instance
		},
		Mac: &mac.Options{
			TitleBar: &mac.TitleBar{
				TitlebarAppearsTransparent: false,
				HideTitle:                  false,
				HideTitleBar:               false,
				FullSizeContent:            false,
				UseToolbar:                 false,
				HideToolbarSeparator:       false,
			},
			Appearance:           mac.NSAppearanceNameDarkAqua,
			WebviewIsTransparent: true,
			WindowIsTranslucent:  false,
			About: &mac.AboutInfo{
				Title:   "magicstick",
				Message: "© 2025 magicstick.",
			},
		},
		SingleInstanceLock: &options.SingleInstanceLock{
			UniqueId: "00099248-875b-4574-bb66-9cdaaf82ded9",
		},
	})

	if err != nil {
		fmt.Fprintf(os.Stderr, "Application failed to start: %v\n", err)
		os.Exit(1)
	}
}
