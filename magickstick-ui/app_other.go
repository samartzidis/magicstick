//go:build !windows

package main

import (
	"fmt"
)

// enableAutostart adds the application to Windows startup registry
func enableAutostart() error {
	return fmt.Errorf("autostart is only supported on Windows")
}

// disableAutostart removes the application from Windows startup registry
func disableAutostart() error {
	return fmt.Errorf("autostart is only supported on Windows")
}

// isAutostartEnabled checks if the application is set to start with Windows
func isAutostartEnabled() (bool, error) {
	return false, fmt.Errorf("autostart is only supported on Windows")
}

// isDarkMode returns true for non-Windows platforms (as requested)
func isDarkMode() bool {
	return false // Default to false on other platforms
}
