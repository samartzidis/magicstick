package hid

import (
	"errors"
	"log/slog"
	"sync"

	"magicstick-ui/usbhid"
)

// ReportHandler defines the interface for handling HID reports
type ReportHandler interface {
	HandleReport(reportID byte, data []byte)
}

// HidDispatcher centralizes HID reading from one or more devices and dispatches reports
// to registered handlers. It normalizes report IDs by putting the report ID in the first byte
// on Windows (Linux/Darwin already include it automatically).
type HidDispatcher struct {
	devices  []*usbhid.Device
	handlers map[byte][]ReportHandler // reportID -> handlers

	stopCh chan struct{}
	wg     sync.WaitGroup
	mu     sync.RWMutex

	// ensure Stop() is idempotent
	stopOnce sync.Once

	// Callback for when a device stops reading (read error/disconnection)
	notifyDisconnect func(deviceIndex int, err error)
}

// NewHidDispatcher creates a new HID dispatcher
func NewHidDispatcher() *HidDispatcher {
	return &HidDispatcher{
		devices:  make([]*usbhid.Device, 0),
		handlers: make(map[byte][]ReportHandler),
		stopCh:   make(chan struct{}),
	}
}

// AddDevice adds a HID device to the dispatcher
func (d *HidDispatcher) AddDevice(dev *usbhid.Device) {
	d.mu.Lock()
	defer d.mu.Unlock()

	if dev != nil && dev.IsOpen() {
		d.devices = append(d.devices, dev)
		slog.Debug("Added device to dispatcher", "path", dev.Path(), "usage", dev.Usage(), "deviceCount", len(d.devices))
	}
}

// RegisterHandler registers a handler for a specific report ID
func (d *HidDispatcher) RegisterHandler(reportID byte, handler ReportHandler) {
	d.mu.Lock()
	defer d.mu.Unlock()

	d.handlers[reportID] = append(d.handlers[reportID], handler)
	slog.Debug("Registered handler for report", "reportID", reportID)
}

// SetReadErrorCallback sets a callback that is called when a device stops reading due to an error
func (d *HidDispatcher) SetReadErrorCallback(cb func(deviceIndex int, err error)) {
	d.mu.Lock()
	defer d.mu.Unlock()

	d.notifyDisconnect = cb
}

// Start begins reading from all registered devices
func (d *HidDispatcher) Start() {
	d.mu.RLock()
	defer d.mu.RUnlock()

	if len(d.devices) == 0 {
		slog.Debug("No devices to start dispatcher")
		return
	}

	slog.Debug("Starting HID dispatcher", "deviceCount", len(d.devices))

	// Start reading from each device
	for i, dev := range d.devices {
		d.wg.Add(1)
		go d.readFromDevice(i, dev)
	}
}

// Stop stops all reading goroutines
func (d *HidDispatcher) Stop() {
	slog.Debug("Stopping HID dispatcher")
	d.stopOnce.Do(func() {
		close(d.stopCh)
	})
	d.wg.Wait()
	slog.Debug("HID dispatcher stopped")
}

// readFromDevice continuously reads from a single device
func (d *HidDispatcher) readFromDevice(deviceIndex int, dev *usbhid.Device) {
	defer d.wg.Done()

	slog.Debug("Started reading from device", "index", deviceIndex, "path", dev.Path())

	// Ensure notifyDisconnect is invoked on any exit path
	notified := false
	defer func() {
		if d.notifyDisconnect != nil && !notified {
			d.notifyDisconnect(deviceIndex, errors.New("reader exited"))
		}
	}()

	for {
		// Use a channel to make GetInputReport cancellable
		resultCh := make(chan struct {
			rid byte
			buf []byte
			err error
		}, 1)

		go func() {
			rid, buf, err := dev.GetInputReport()
			resultCh <- struct {
				rid byte
				buf []byte
				err error
			}{rid, buf, err}
		}()

		select {
		case <-d.stopCh:
			slog.Debug("Device reader stop signal received", "index", deviceIndex)
			return
		case result := <-resultCh:
			if result.err != nil {
				slog.Error("Device reading error, stopping reader", "index", deviceIndex, "error", result.err, "path", dev.Path(), "usage", dev.Usage())

				return
			}

			if len(result.buf) == 0 {
				continue
			}

			// Normalize report data - Linux includes report ID in first byte automatically,
			// Windows needs explicit normalization. But handlers should only receive the actual payload.
			var normalizedData []byte
			if isCompositePlatform {
				// Linux/Darwin: report ID is already in first byte, skip it
				if len(result.buf) > 0 && result.buf[0] == result.rid {
					normalizedData = result.buf[1:] // Skip the report ID
				} else {
					normalizedData = result.buf // Use as-is if no report ID
				}
			} else {
				// Windows: use data as-is (report ID is separate)
				normalizedData = result.buf
			}

			slog.Debug("Report received", "deviceIndex", deviceIndex, "rid", result.rid, "length", len(normalizedData))

			// Dispatch to registered handlers
			d.dispatchReport(result.rid, normalizedData)
		}
	}
}

// dispatchReport sends the report to all registered handlers for the report ID
func (d *HidDispatcher) dispatchReport(reportID byte, data []byte) {
	d.mu.RLock()
	defer d.mu.RUnlock()

	handlers := d.handlers[reportID]
	if len(handlers) == 0 {
		slog.Debug("No handlers registered for report", "reportID", reportID)
		return
	}

	for _, handler := range handlers {
		handler.HandleReport(reportID, data)
	}
}
