//go:build !windows

package systray

import (
	"log/slog"
)

func runPlatformSpecific(tray *SystemTray, onReady func(), onExit func()) {
	slog.Info("System tray not supported on this platform")

	if onReady != nil {
		onReady()
	}

	// Wait for context cancellation
	<-tray.ctx.Done()

	if onExit != nil {
		onExit()
	}
}

// setIconPlatform sets the tray icon (default implementation)
func (s *SystemTray) setIconPlatform(icon []byte) {
	// No-op for unsupported platforms
}

// setTooltipPlatform sets the tray tooltip (default implementation)
func (s *SystemTray) setTooltipPlatform(tooltip string) {
	// No-op for unsupported platforms
}

// quitPlatform removes the tray icon (default implementation)
func (s *SystemTray) quitPlatform() {
	// No-op for unsupported platforms
}
