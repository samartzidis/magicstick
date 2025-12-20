//go:build !windows

package main

// setAutostart is a no-op on non-Windows platforms
func setAutostart(enabled bool) error {
	return nil
}

// getAutostartStatus always returns false on non-Windows platforms
func getAutostartStatus() (bool, error) {
	return false, nil
}

// isDarkMode always returns true on non-Windows platforms
func isDarkMode() bool {
	return true
}

