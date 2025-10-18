package hid

import (
	"crypto/rand"
	"encoding/json"
	"errors"
	"fmt"
	"log/slog"
	"strings"
	"sync"
	"time"

	"magicstick-ui/usbhid"
)

// RPC over HidIo (ReportIdHidIo) with chunking: header byte encodes length (6 bits) and more-data flag.
type DeviceRpc struct {
	dev *usbhid.Device // hidio interface, opened

	// read loop
	stopCh chan struct{}

	// receive assembly
	recvMu    sync.Mutex
	recvBytes []byte

	// in-flight calls
	callsMu sync.Mutex
	calls   map[string]chan string

	// RPC serialization - prevent parallel calls
	rpcBusy   bool
	rpcBusyMu sync.Mutex

	// event callback
	onEvent func(name string, payload string)
}

func NewDeviceRpc(dev *usbhid.Device) (*DeviceRpc, error) {
	if dev == nil || !dev.IsOpen() {
		return nil, errors.New("hidio device not open")
	}
	r := &DeviceRpc{
		dev:    dev,
		stopCh: make(chan struct{}),
		calls:  make(map[string]chan string),
	}
	return r, nil
}

// generateUUID generates a UUID-like string similar to C# Guid.NewGuid().ToString()
func generateUUID() string {
	b := make([]byte, 16)
	rand.Read(b)
	return fmt.Sprintf("%x-%x-%x-%x-%x", b[0:4], b[4:6], b[6:8], b[8:10], b[10:])
}

func (r *DeviceRpc) SetEventCallback(cb func(name string, payload string)) { r.onEvent = cb }

// HandleReport implements ReportHandler interface for HID dispatcher
func (r *DeviceRpc) HandleReport(reportID byte, data []byte) {
	if reportID == ReportIdHidIo {
		slog.Debug("RPC report received via dispatcher", "rid", reportID, "length", len(data))

		// Process RPC data (same logic as before but without the goroutine)
		if len(data) == 0 {
			return
		}

		// Handle RPC responses and events
		r.processRpcData(data)
	}
}

// processRpcData handles incoming RPC data
func (r *DeviceRpc) processRpcData(data []byte) {
	// On Linux, the report ID is included in the first byte of data
	// On Windows, the report ID is separate
	var dataStart int
	if data[0] == ReportIdHidIo {
		// Linux: report ID is in first byte, actual data starts at index 1
		dataStart = 1
	} else {
		// Windows: data starts at index 0
		dataStart = 0
	}

	if len(data) <= dataStart {
		return
	}

	hdr := data[dataStart]
	length := int(hdr & 0x3f)
	more := (hdr & 0x40) != 0
	if length > len(data)-dataStart-1 {
		length = len(data) - dataStart - 1
	}
	chunk := data[dataStart+1 : dataStart+1+length]
	r.recvMu.Lock()
	r.recvBytes = append(r.recvBytes, chunk...)
	done := !more
	payload := make([]byte, len(r.recvBytes))
	copy(payload, r.recvBytes)
	slog.Debug("RPC chunk", "more", more, "chunk_len", len(chunk), "total_len", len(payload), "chunk", string(chunk))
	slog.Debug("RPC chunk bytes", "bytes", chunk)
	if done {
		slog.Debug("RPC complete payload", "payload", string(payload))
		r.recvBytes = r.recvBytes[:0]
	}
	r.recvMu.Unlock()
	if done {
		r.handlePayload(payload)
	}
}

func (r *DeviceRpc) handlePayload(data []byte) {
	slog.Debug("Received payload", "payload", string(data))
	var obj map[string]any
	if err := json.Unmarshal(data, &obj); err != nil {
		slog.Error("Failed to parse payload", "error", err, "payload", string(data))

		// Try to extract ID from malformed JSON for error responses
		payloadStr := string(data)
		if strings.Contains(payloadStr, `"id":`) && strings.Contains(payloadStr, `"error":`) {
			// Try to extract ID using string manipulation for malformed JSON
			if idStart := strings.Index(payloadStr, `"id":"`); idStart != -1 {
				idStart += 6 // Skip `"id":"`
				if idEnd := strings.Index(payloadStr[idStart:], `"`); idEnd != -1 {
					id := payloadStr[idStart : idStart+idEnd]
					slog.Debug("Extracted ID from malformed JSON", "id", id)
					r.callsMu.Lock()
					ch, ok := r.calls[id]
					if ok {
						delete(r.calls, id)
						slog.Debug("Found and removed RPC call from malformed JSON", "id", id, "remaining_calls", len(r.calls))
					}
					r.callsMu.Unlock()
					if ok {
						ch <- string(data)
					}
				}
			}
		}
		return
	}
	if v, ok := obj["event_name"].(string); ok && v != "" {
		if r.onEvent != nil {
			r.onEvent(v, string(data))
		}
		return
	}
	if id, ok := obj["id"].(string); ok && id != "" {
		slog.Debug("Received RPC response", "id", id)
		r.callsMu.Lock()
		ch, ok := r.calls[id]
		if ok {
			delete(r.calls, id)
			slog.Debug("Found and removed RPC call", "id", id, "remaining_calls", len(r.calls))
		} else {
			slog.Debug("No RPC call found", "id", id)
		}
		r.callsMu.Unlock()
		if ok {
			ch <- string(data)
		}
	}
}

// Send JSON via HidIo chunked output reports.
func (r *DeviceRpc) sendJSON(b []byte) error {
	// chunk payload into 32-byte data chunks + 1-byte header at position 0
	n := (len(b) + 31) / 32
	for i := 0; i < n; i++ {
		start := i * 32
		end := start + 32
		if end > len(b) {
			end = len(b)
		}
		dataLen := end - start
		header := byte(dataLen & 0x3f)
		if i < n-1 {
			header |= 0x40
		}

		// Create fixed 33-byte chunk like C# version
		chunk := make([]byte, 33)
		chunk[0] = header
		copy(chunk[1:], b[start:end])

		// Debug: Log chunk being sent
		slog.Debug("Sending chunk", "chunk_num", i+1, "total_chunks", n, "header", fmt.Sprintf("0x%02x", header), "data_len", dataLen, "data", string(chunk[1:1+dataLen]))

		// Use the chunk directly for SetOutputReport
		if err := r.dev.SetOutputReport(ReportIdHidIo, chunk); err != nil {
			return err
		}
	}
	return nil
}

// RpcCall sends a request object (with fields method_name and id) and waits for reply.
func (r *DeviceRpc) RpcCall(req any, id string, timeout time.Duration) (string, error) {
	if id == "" {
		return "", errors.New("id required")
	}

	// Check if another RPC call is already in progress
	r.rpcBusyMu.Lock()
	if r.rpcBusy {
		r.rpcBusyMu.Unlock()
		slog.Debug("RPC call rejected - another call already in progress", "id", id)
		return "", errors.New("RPC call already in progress")
	}
	r.rpcBusy = true
	r.rpcBusyMu.Unlock()

	// Ensure we clear the busy flag when done
	defer func() {
		r.rpcBusyMu.Lock()
		r.rpcBusy = false
		r.rpcBusyMu.Unlock()
		slog.Debug("RPC call completed, busy flag cleared", "id", id)
	}()

	data, err := json.Marshal(req)
	if err != nil {
		return "", err
	}

	// Debug: Log the JSON being sent
	slog.Debug("Sending RPC JSON", "data", string(data))

	r.callsMu.Lock()
	ch := make(chan string, 1)
	r.calls[id] = ch
	slog.Debug("Registered RPC call", "id", id, "total_calls", len(r.calls))
	r.callsMu.Unlock()
	if err := r.sendJSON(data); err != nil {
		// Clean up the call on error
		r.callsMu.Lock()
		delete(r.calls, id)
		r.callsMu.Unlock()
		return "", err
	}

	// Use a more sophisticated timeout mechanism like C#
	startTime := time.Now()
	for {
		select {
		case resp := <-ch:
			return resp, nil
		case <-time.After(1 * time.Second):
			// Check if we've exceeded the total timeout
			if time.Since(startTime) > timeout {
				return "", errors.New("rpc timeout")
			}
			// Continue waiting if we haven't exceeded timeout
		}
	}
}

// Typed helpers
type rpcBase struct {
	MethodName string `json:"method_name"`
	Id         string `json:"id"`
}

type GetSettingsRequest struct{ rpcBase }
type GetSettingsReply struct {
	Id                string `json:"id"`
	SwapFnCtrl        bool   `json:"swap_fn_ctrl"`
	SwapAltCmd        bool   `json:"swap_alt_cmd"`
	BluetoothDisabled bool   `json:"bluetooth_disabled"`
	IoTiming          uint32 `json:"io_timing"`
}

type SetSettingsRequest struct {
	rpcBase
	SwapFnCtrl        bool   `json:"swap_fn_ctrl"`
	SwapAltCmd        bool   `json:"swap_alt_cmd"`
	BluetoothDisabled bool   `json:"bluetooth_disabled"`
	IoTiming          uint32 `json:"io_timing"`
}

type SaveConfigRequest struct{ rpcBase }

type GetKeymapRequest struct {
	rpcBase
	Defaults bool `json:"defaults"`
}

type GetKeymapReply struct {
	Id    string   `json:"id"`
	Items []string `json:"items"`
}

type SetKeymapRequest struct {
	rpcBase
	Items []string `json:"items"`
}

type SetKeymapReply struct {
	Id      string `json:"id"`
	Success bool   `json:"success"`
	Error   string `json:"error"`
}

// RPC Event types
type RpcEvent struct {
	EventName string `json:"event_name"`
}

type SendUnicodeCharEvent struct {
	RpcEvent
	KeyCode int `json:"key_code"`
}

func (r *DeviceRpc) GetSettings(timeout time.Duration) (*GetSettingsReply, error) {
	id := generateUUID()
	slog.Debug("GetSettings called", "id", id)
	req := GetSettingsRequest{rpcBase{MethodName: "get_settings", Id: id}}
	s, err := r.RpcCall(req, id, timeout)
	if err != nil {
		return nil, err
	}
	var rep GetSettingsReply
	if err := json.Unmarshal([]byte(s), &rep); err != nil {
		return nil, err
	}
	return &rep, nil
}

func (r *DeviceRpc) SetSettings(req SetSettingsRequest, timeout time.Duration) error {
	if req.Id == "" {
		req.Id = generateUUID()
	}
	req.MethodName = "set_settings"

	data, err := json.Marshal(req)
	if err != nil {
		return err
	}

	slog.Debug("Sending SetSettings (fire-and-forget)", "data", string(data))
	return r.sendJSON(data)
}

func (r *DeviceRpc) SaveConfig(timeout time.Duration) error {
	id := generateUUID()
	req := SaveConfigRequest{rpcBase{MethodName: "save_config", Id: id}}

	data, err := json.Marshal(req)
	if err != nil {
		return err
	}

	slog.Debug("Sending SaveConfig (fire-and-forget)", "data", string(data))
	return r.sendJSON(data)
}

func (r *DeviceRpc) GetKeymap(defaults bool, timeout time.Duration) (*GetKeymapReply, error) {
	id := generateUUID()
	req := GetKeymapRequest{}
	req.MethodName = "get_keymap"
	req.Id = id
	req.Defaults = defaults
	s, err := r.RpcCall(req, id, timeout)
	if err != nil {
		return nil, err
	}
	var rep GetKeymapReply
	if err := json.Unmarshal([]byte(s), &rep); err != nil {
		return nil, err
	}
	return &rep, nil
}

func (r *DeviceRpc) SetKeymap(items []string, timeout time.Duration) (*SetKeymapReply, error) {
	id := generateUUID()
	req := SetKeymapRequest{}
	req.MethodName = "set_keymap"
	req.Id = id
	req.Items = items
	s, err := r.RpcCall(req, id, timeout)
	if err != nil {
		return nil, err
	}
	var rep SetKeymapReply
	if err := json.Unmarshal([]byte(s), &rep); err != nil {
		slog.Error("Failed to unmarshal SetKeymapReply", "error", err, "payload", s)

		// Try to extract error message from malformed JSON
		payloadStr := s
		if strings.Contains(payloadStr, `"error":`) {
			// Try to extract error message using string manipulation
			if errorStart := strings.Index(payloadStr, `"error":"`); errorStart != -1 {
				errorStart += 8 // Skip `"error":"`
				if errorEnd := strings.Index(payloadStr[errorStart:], `"`); errorEnd != -1 {
					errorMsg := payloadStr[errorStart : errorStart+errorEnd]
					slog.Debug("Extracted error message from malformed JSON", "error", errorMsg)
					return &SetKeymapReply{
						Id:      id,
						Success: false,
						Error:   errorMsg,
					}, nil
				}
			}
		}

		return nil, fmt.Errorf("failed to parse response: %v", err)
	}
	return &rep, nil
}
