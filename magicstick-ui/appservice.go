package main

import (
	"bytes"
	"embed"
	"fmt"
	"image"
	"image/color"
	"image/png"
	"log/slog"

	"github.com/wailsapp/wails/v3/pkg/application"
	"gopkg.in/yaml.v3"
)

//go:embed assets/icons/Battery.png assets/icons/Battery_dark.png assets/icons/Indicator_*.png assets/icons/Missing.png assets/icons/Missing_dark.png assets/icons/Charging.png assets/icons/Charging_dark.png assets/icons/Charged.png assets/icons/Charged_dark.png
var batteryIcons embed.FS

//go:embed build/config.yml
var buildConfigData []byte

// BuildConfig represents the structure of build/config.yml
type BuildConfig struct {
	Info struct {
		Version string `yaml:"version"`
	} `yaml:"info"`
}

// AppService handles application-level operations
type AppService struct {
	app        *application.App
	systemTray *application.SystemTray
}

// NewAppService creates a new AppService
func NewAppService(app *application.App) *AppService {
	return &AppService{
		app: app,
	}
}

// SetSystemTray sets the system tray reference
func (a *AppService) SetSystemTray(systemTray *application.SystemTray) {
	a.systemTray = systemTray
}

// GetVersion returns the application version from build/config.yml
func (a *AppService) GetVersion() string {
	var config BuildConfig
	if err := yaml.Unmarshal(buildConfigData, &config); err != nil {
		slog.Error("Failed to parse build config", "error", err)
		return "unknown"
	}
	return config.Info.Version
}

// SetAutostart enables or disables autostart for the application
func (a *AppService) SetAutostart(enabled bool) error {
	return setAutostart(enabled)
}

// GetAutostartStatus returns whether autostart is currently enabled
func (a *AppService) GetAutostartStatus() (bool, error) {
	return getAutostartStatus()
}

// IsDarkMode returns true if Windows uses dark mode theme, true for any other OS
func (a *AppService) IsDarkMode() bool {
	return isDarkMode()
}

// LoadIconData loads icon data from the embedded filesystem
// Returns the raw icon data that can be used for blending
func (a *AppService) LoadIconData(iconPath string) ([]byte, error) {
	slog.Debug("Loading icon", "path", iconPath)

	iconData, err := batteryIcons.ReadFile(iconPath)
	if err != nil {
		return nil, fmt.Errorf("failed to read icon %s: %v", iconPath, err)
	}

	return iconData, nil
}

// BlendIconData blends multiple icon data arrays and returns the result
// Takes icon paths and returns blended icon data as PNG bytes
func (a *AppService) BlendIconData(iconPaths []string) ([]byte, error) {
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

	// Convert to PNG format
	return a.convertToPNG(blendedImg)
}

// convertToPNG converts an image to PNG format
func (a *AppService) convertToPNG(img image.Image) ([]byte, error) {
	var buf bytes.Buffer
	err := png.Encode(&buf, img)
	if err != nil {
		return nil, fmt.Errorf("failed to encode PNG: %v", err)
	}
	return buf.Bytes(), nil
}

// blendImages dynamically blends multiple images together
func (a *AppService) blendImages(images ...image.Image) image.Image {
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

	// Draw subsequent images centered on top
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

// UpdateTrayIcon updates the system tray icon with the provided PNG icon data
func (a *AppService) UpdateTrayIcon(iconData []byte) error {
	if a.systemTray == nil {
		return fmt.Errorf("system tray is nil")
	}

	// Update the tray icon with the blended icon data
	a.systemTray.SetIcon(iconData)
	a.systemTray.SetDarkModeIcon(iconData) // Use same icon for both modes

	slog.Debug("Updated system tray icon")
	return nil
}

// UpdateTrayTooltip updates the system tray tooltip text
func (a *AppService) UpdateTrayTooltip(tooltip string) error {
	if a.systemTray == nil {
		return fmt.Errorf("system tray is nil")
	}

	a.systemTray.SetTooltip(tooltip)
	return nil
}
