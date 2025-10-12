package main

import (
	"context"
	"fmt"
	"log/slog"
	"os"
	"runtime"
	"strings"

	wailsRuntime "github.com/wailsapp/wails/v2/pkg/runtime"
)

// Global context for Wails runtime logging
var wailsCtx context.Context

// SetWailsLoggingContext sets the Wails context for logging
func SetWailsLoggingContext(ctx context.Context) {
	wailsCtx = ctx

	slog.SetDefault(slog.New(&customFormatter{
		handler: slog.NewTextHandler(os.Stdout, &slog.HandlerOptions{
			Level:     slog.LevelDebug, // Set to slog.LevelDebug so that we dont filter anything since Handle does that
			AddSource: true,
		}),
	}))
}

// customFormatter wraps the default handler but changes only the formatting
type customFormatter struct {
	handler slog.Handler
}

func (f *customFormatter) Enabled(ctx context.Context, level slog.Level) bool {
	return f.handler.Enabled(ctx, level)
}

func (f *customFormatter) Handle(ctx context.Context, record slog.Record) error {
	// Format timestamp with milliseconds
	timestamp := record.Time.Format("15:04:05.000")

	// Format source (file:line)
	var sourceStr string
	if record.PC != 0 {
		fs := runtime.CallersFrames([]uintptr{record.PC})
		frame, _ := fs.Next()
		if frame.File != "" {
			// Extract just the filename from the full path
			parts := strings.Split(frame.File, "/")
			filename := parts[len(parts)-1]
			sourceStr = fmt.Sprintf("%s:%d", filename, frame.Line)
		}
	}

	// Build the message with key-value pairs
	message := record.Message
	record.Attrs(func(a slog.Attr) bool {
		if a.Key != "" && a.Value.Kind() != slog.KindAny {
			message += fmt.Sprintf(" %s=%v", a.Key, a.Value)
		}
		return true
	})

	// Forward to Wails logger if context is available
	if wailsCtx != nil {
		// Format message without level (Wails adds its own level prefix)
		var logMessage string
		if sourceStr != "" {
			logMessage = fmt.Sprintf("[%s] | %s | %s", sourceStr, timestamp, message)
		} else {
			logMessage = fmt.Sprintf("%s | %s", timestamp, message)
		}

		switch record.Level {
		case slog.LevelDebug:
			wailsRuntime.LogDebug(wailsCtx, logMessage)
		case slog.LevelInfo:
			wailsRuntime.LogInfo(wailsCtx, logMessage)
		case slog.LevelWarn:
			wailsRuntime.LogWarning(wailsCtx, logMessage)
		case slog.LevelError:
			wailsRuntime.LogError(wailsCtx, logMessage)
		default:
			wailsRuntime.LogPrint(wailsCtx, logMessage)
		}
	} else {
		// Wails context not available - this should not happen in normal operation
		return fmt.Errorf("wails logging context not set")
	}

	return nil
}

func (f *customFormatter) WithAttrs(attrs []slog.Attr) slog.Handler {
	// Delegate to the wrapped handler to maintain proper behavior
	return &customFormatter{
		handler: f.handler.WithAttrs(attrs),
	}
}

func (f *customFormatter) WithGroup(name string) slog.Handler {
	// Delegate to the wrapped handler to maintain proper behavior
	return &customFormatter{
		handler: f.handler.WithGroup(name),
	}
}
