//go:build windows

package keyboard_input

import (
	"fmt"
	"log/slog"
	"runtime"
	"unsafe"

	"golang.org/x/sys/windows"
)

const (
	INPUT_KEYBOARD    = 1
	KEYEVENTF_KEYUP   = 0x0002
	KEYEVENTF_UNICODE = 0x0004
)

type INPUT struct {
	Type uint32
	Ki   KEYBDINPUT
	_    [4]byte // padding to match C struct alignment
}

type KEYBDINPUT struct {
	WVk         uint16
	WScan       uint16
	DwFlags     uint32
	Time        uint32
	DwExtraInfo uintptr
}

var (
	user32                  = windows.NewLazySystemDLL("user32.dll")
	procSendInput           = user32.NewProc("SendInput")
	procGetMessageExtraInfo = user32.NewProc("GetMessageExtraInfo")
)

func handleSendUnicodeCharEvent(keyCode int) error {
	slog.Debug("Windows handleSendUnicodeCharEvent called", "key_code", keyCode)

	// Check if we're on Windows
	if runtime.GOOS != "windows" {
		slog.Debug("SendUnicodeCharEvent not supported", "platform", runtime.GOOS)
		return fmt.Errorf("SendUnicodeCharEvent is only supported on Windows")
	}

	// Send the Unicode character to the active window
	slog.Debug("Calling sendUnicodeToActiveWindow", "code", keyCode)
	err := sendUnicodeToActiveWindow(keyCode)
	if err != nil {
		slog.Error("sendUnicodeToActiveWindow failed", "error", err)
		return err
	}

	slog.Debug("sendUnicodeToActiveWindow completed successfully")
	return nil
}

func sendUnicodeToActiveWindow(unicodeValue int) error {
	slog.Debug("Windows sendUnicodeToActiveWindow called",
		"unicode_value", unicodeValue,
		"character", string(rune(unicodeValue)),
	)

	var inputs []INPUT

	// Get surrogate pairs for the unicode value
	surrogatePairs := unicodeToUtf16SurrogatePairs(unicodeValue)
	slog.Debug("Generated surrogate pairs", "pairs", surrogatePairs)

	for i, pair := range surrogatePairs {
		slog.Debug("Processing surrogate pair", "index", i, "pair", pair)

		// Key down event
		inputs = append(inputs, INPUT{
			Type: INPUT_KEYBOARD,
			Ki: KEYBDINPUT{
				WVk:         0,
				WScan:       pair,
				DwFlags:     KEYEVENTF_UNICODE,
				DwExtraInfo: getMessageExtraInfo(),
			},
		})

		// Key up event
		inputs = append(inputs, INPUT{
			Type: INPUT_KEYBOARD,
			Ki: KEYBDINPUT{
				WVk:         0,
				WScan:       pair,
				DwFlags:     KEYEVENTF_UNICODE | KEYEVENTF_KEYUP,
				DwExtraInfo: getMessageExtraInfo(),
			},
		})
	}

	slog.Debug("Created input events", "count", len(inputs))

	if len(inputs) == 0 {
		slog.Debug("No inputs to send")
		return nil
	}

	slog.Debug("Calling SendInput", "input_count", len(inputs))
	ret, _, err := procSendInput.Call(
		uintptr(len(inputs)),
		uintptr(unsafe.Pointer(&inputs[0])),
		uintptr(unsafe.Sizeof(INPUT{})),
	)

	slog.Debug("SendInput returned", "ret", ret, "err", err)

	if ret == 0 {
		return fmt.Errorf("SendInput failed: %v", err)
	}

	slog.Debug("SendInput succeeded")
	return nil
}

// getMessageExtraInfo gets the extra info for input messages
func getMessageExtraInfo() uintptr {
	ret, _, _ := procGetMessageExtraInfo.Call()
	return ret
}
