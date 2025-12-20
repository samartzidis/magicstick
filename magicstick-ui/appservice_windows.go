//go:build windows

package main

import (
	"fmt"
	"os"
	"path/filepath"

	"golang.org/x/sys/windows/registry"
)

// setAutostart enables or disables autostart for the application
func setAutostart(enabled bool) error {
	if enabled {
		return enableAutostart()
	} else {
		return disableAutostart()
	}
}

// enableAutostart adds the application to Windows startup registry
func enableAutostart() error {
	key, err := registry.OpenKey(registry.CURRENT_USER, `SOFTWARE\Microsoft\Windows\CurrentVersion\Run`, registry.QUERY_VALUE|registry.SET_VALUE)
	if err != nil {
		return fmt.Errorf("failed to open registry key: %v", err)
	}
	defer key.Close()

	exePath, err := os.Executable()
	if err != nil {
		return fmt.Errorf("failed to get executable path: %v", err)
	}

	// Convert to absolute path
	exePath, err = filepath.Abs(exePath)
	if err != nil {
		return fmt.Errorf("failed to get absolute path: %v", err)
	}

	err = key.SetStringValue("MagicStickUI", exePath)
	if err != nil {
		return fmt.Errorf("failed to set registry value: %v", err)
	}

	return nil
}

// disableAutostart removes the application from Windows startup registry
func disableAutostart() error {
	key, err := registry.OpenKey(registry.CURRENT_USER, `SOFTWARE\Microsoft\Windows\CurrentVersion\Run`, registry.QUERY_VALUE|registry.SET_VALUE)
	if err != nil {
		return fmt.Errorf("failed to open registry key: %v", err)
	}
	defer key.Close()

	err = key.DeleteValue("MagicStickUI")
	if err != nil {
		return fmt.Errorf("failed to delete registry value: %v", err)
	}

	return nil
}

// getAutostartStatus checks if the application is set to start with Windows
func getAutostartStatus() (bool, error) {
	key, err := registry.OpenKey(registry.CURRENT_USER, `SOFTWARE\Microsoft\Windows\CurrentVersion\Run`, registry.QUERY_VALUE|registry.SET_VALUE)
	if err != nil {
		return false, fmt.Errorf("failed to open registry key: %v", err)
	}
	defer key.Close()

	_, _, err = key.GetStringValue("MagicStickUI")
	if err != nil {
		if err == registry.ErrNotExist {
			return false, nil
		}
		return false, fmt.Errorf("failed to get registry value: %v", err)
	}

	return true, nil
}

// isDarkMode checks if Windows is using dark mode theme
func isDarkMode() bool {
	// Check the Windows registry for dark mode setting
	key, err := registry.OpenKey(registry.CURRENT_USER, `SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize`, registry.QUERY_VALUE)
	if err != nil {
		// If we can't access the registry, default to dark mode
		return true
	}
	defer key.Close()

	// Get the AppsUseLightTheme value (0 = dark mode, 1 = light mode)
	value, _, err := key.GetIntegerValue("AppsUseLightTheme")
	if err != nil {
		// If we can't read the value, default to dark mode
		return true
	}

	// Return true if dark mode (value == 0), false if light mode (value == 1)
	return value == 0
}

