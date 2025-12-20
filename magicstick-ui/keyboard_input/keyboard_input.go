package keyboard_input

import (
	"encoding/json"
	"fmt"
	"log/slog"
)

// SendUnicodeCharEvent represents a Unicode character event from the device
type SendUnicodeCharEvent struct {
	EventName string `json:"event_name"`
	KeyCode   int    `json:"key_code"`
}

// Handler interface for processing Unicode character events
type Handler interface {
	HandleSendUnicodeCharEvent(payload string) error
}

// HandleSendUnicodeCharEvent processes a SendUnicodeCharEvent from the device
func HandleSendUnicodeCharEvent(payload string) error {
	var event SendUnicodeCharEvent
	if err := json.Unmarshal([]byte(payload), &event); err != nil {
		slog.Error("Failed to unmarshal SendUnicodeCharEvent",
			"error", err,
			"payload", payload,
		)
		return fmt.Errorf("failed to unmarshal SendUnicodeCharEvent: %v", err)
	}

	slog.Debug("Handling SendUnicodeCharEvent",
		"key_code", event.KeyCode,
		"unicode_char", string(rune(event.KeyCode)),
	)

	// Delegate to platform-specific implementation
	return handleSendUnicodeCharEvent(event.KeyCode)
}

// SendStringToActiveWindow sends a string to the active window
func SendStringToActiveWindow(s string) error {
	slog.Debug("SendStringToActiveWindow called", "string", s)

	for _, c := range s {
		err := SendUnicodeToActiveWindow(int(c))
		if err != nil {
			slog.Error("Failed to send character",
				"error", err,
				"character", string(c),
				"unicode_value", int(c),
			)
			return fmt.Errorf("failed to send character %c: %v", c, err)
		}
	}

	return nil
}

// SendUnicodeToActiveWindow sends a Unicode code point to the active window
func SendUnicodeToActiveWindow(unicodeValue int) error {
	slog.Debug("SendUnicodeToActiveWindow called",
		"unicode_value", unicodeValue,
		"character", string(rune(unicodeValue)),
	)

	// Delegate to platform-specific implementation
	return sendUnicodeToActiveWindow(unicodeValue)
}

// unicodeToUtf16SurrogatePairs converts a Unicode code point to UTF-16 surrogate pairs
// This function handles characters outside the Basic Multilingual Plane (BMP)
func unicodeToUtf16SurrogatePairs(codePoint int) []uint16 {
	var surrogatePairs []uint16

	slog.Debug("Processing Unicode code point",
		"code_point", codePoint,
		"hex", fmt.Sprintf("0x%04X", codePoint),
	)

	if codePoint >= 0x10000 && codePoint <= 0x10FFFF {
		// Characters outside BMP require surrogate pairs
		slog.Debug("Character is outside BMP, generating surrogate pairs")
		codePoint -= 0x10000
		highSurrogate := uint16((codePoint >> 10) + 0xD800)
		lowSurrogate := uint16((codePoint & 0x3FF) + 0xDC00)

		surrogatePairs = append(surrogatePairs, highSurrogate)
		surrogatePairs = append(surrogatePairs, lowSurrogate)
		slog.Debug("Generated surrogate pairs",
			"high_surrogate", fmt.Sprintf("0x%04X", highSurrogate),
			"low_surrogate", fmt.Sprintf("0x%04X", lowSurrogate),
		)
	} else {
		// Characters within BMP can be represented directly
		slog.Debug("Character is within BMP, using direct representation")
		surrogatePairs = append(surrogatePairs, uint16(codePoint))
		slog.Debug("Direct representation",
			"value", fmt.Sprintf("0x%04X", uint16(codePoint)),
		)
	}

	return surrogatePairs
}
