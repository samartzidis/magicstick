//go:build !windows

package keyboard_input

import (
	"fmt"
	"log/slog"
	"runtime"
)

func handleSendUnicodeCharEvent(keyCode int) error {
	slog.Debug("Default handleSendUnicodeCharEvent called", "key_code", keyCode)

	slog.Debug("SendUnicodeCharEvent not supported", "platform", runtime.GOOS)
	return fmt.Errorf("SendUnicodeCharEvent is only supported on Windows")
}

func sendUnicodeToActiveWindow(unicodeValue int) error {
	slog.Debug("Default sendUnicodeToActiveWindow called",
		"unicode_value", unicodeValue,
		"character", string(rune(unicodeValue)),
	)

	return fmt.Errorf("SendUnicodeToActiveWindow is only supported on Windows")
}
