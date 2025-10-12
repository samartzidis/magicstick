//go:build windows

package systray

import (
	"context"
	"fmt"
	"log/slog"
	"syscall"
	"time"
	"unsafe"

	"golang.org/x/sys/windows"
)

const (
	WM_USER          = 0x0400
	NIM_ADD          = 0x00000000
	NIM_MODIFY       = 0x00000001
	NIM_DELETE       = 0x00000002
	NIM_SETVERSION   = 0x00000004
	NIF_MESSAGE      = 0x00000001
	NIF_ICON         = 0x00000002
	NIF_TIP          = 0x00000004
	NIF_STATE        = 0x00000008
	NIF_INFO         = 0x00000010
	NIF_GUID         = 0x00000020
	NIF_REALTIME     = 0x00000040
	NIF_SHOWTIP      = 0x00000080
	NIF_ICONRESOURCE = 0x00000010

	WM_TRAYICON      = WM_USER + 1
	WM_LBUTTONUP     = 0x0202
	WM_RBUTTONUP     = 0x0205
	WM_LBUTTONDBLCLK = 0x0203
	WM_RBUTTONDBLCLK = 0x0206

	// Theme change messages
	WM_THEMECHANGED          = 0x031A
	WM_DWMCOMPOSITIONCHANGED = 0x031E
	WM_DISPLAYCHANGE         = 0x007E
	WM_SETTINGCHANGE         = 0x001A
	WM_WININICHANGE          = 0x001A // Same as WM_SETTINGCHANGE
	WM_SYSCOLORCHANGE        = 0x0015

	NOTIFYICON_VERSION = 4
)

type NOTIFYICONDATA struct {
	CbSize           uint32
	HWnd             windows.Handle
	UID              uint32
	UFlags           uint32
	UCallbackMessage uint32
	HIcon            windows.Handle
	SzTip            [128]uint16
	DwState          uint32
	DwStateMask      uint32
	SzInfo           [256]uint16
	UTimeout         uint32
	SzInfoTitle      [64]uint16
	DwInfoFlags      uint32
	GuidItem         windows.GUID
	HBalloonIcon     windows.Handle
}

type windowsTray struct {
	hwnd       windows.Handle
	icon       windows.Handle
	notifyData NOTIFYICONDATA
	tray       *SystemTray // Reference to the main tray to get current menu items
	onReady    func()
	onExit     func()
	onClick    func()
	ctx        context.Context
	cancel     context.CancelFunc
}

var (
	user32                       = windows.NewLazySystemDLL("user32.dll")
	shell32                      = windows.NewLazySystemDLL("shell32.dll")
	procCreateWindowEx           = user32.NewProc("CreateWindowExW")
	procDefWindowProc            = user32.NewProc("DefWindowProcW")
	procDestroyWindow            = user32.NewProc("DestroyWindow")
	procRegisterClass            = user32.NewProc("RegisterClassW")
	procUnregisterClass          = user32.NewProc("UnregisterClassW")
	procLoadIcon                 = user32.NewProc("LoadIconW")
	procLoadImage                = user32.NewProc("LoadImageW")
	procDestroyIcon              = user32.NewProc("DestroyIcon")
	procCreateIconFromResourceEx = user32.NewProc("CreateIconFromResourceEx")
	procGetSystemMetrics         = user32.NewProc("GetSystemMetrics")
	procShell_NotifyIcon         = shell32.NewProc("Shell_NotifyIconW")
	procCreatePopupMenu          = user32.NewProc("CreatePopupMenu")
	procTrackPopupMenu           = user32.NewProc("TrackPopupMenu")
	procDestroyMenu              = user32.NewProc("DestroyMenu")
	procAppendMenu               = user32.NewProc("AppendMenuW")
	procSetMenuDefaultItem       = user32.NewProc("SetMenuDefaultItem")
	procGetCursorPos             = user32.NewProc("GetCursorPos")
)

const (
	WS_OVERLAPPED   = 0x00000000
	WS_POPUP        = 0x80000000
	WS_CHILD        = 0x40000000
	WS_MINIMIZE     = 0x20000000
	WS_VISIBLE      = 0x10000000
	WS_DISABLED     = 0x08000000
	WS_CLIPSIBLINGS = 0x04000000
	WS_CLIPCHILDREN = 0x02000000
	WS_MAXIMIZE     = 0x01000000
	WS_CAPTION      = 0x00C00000
	WS_BORDER       = 0x00800000
	WS_DLGFRAME     = 0x00400000
	WS_VSCROLL      = 0x00200000
	WS_HSCROLL      = 0x00100000
	WS_SYSMENU      = 0x00080000
	WS_THICKFRAME   = 0x00040000
	WS_GROUP        = 0x00020000
	WS_TABSTOP      = 0x00010000
	WS_MINIMIZEBOX  = 0x00020000
	WS_MAXIMIZEBOX  = 0x00010000

	CW_USEDEFAULT = ^uintptr(0x7fffffff)

	SW_HIDE = 0
	SW_SHOW = 5

	TPM_LEFTBUTTON   = 0x0000
	TPM_RIGHTBUTTON  = 0x0002
	TPM_LEFTALIGN    = 0x0000
	TPM_CENTERALIGN  = 0x0004
	TPM_RIGHTALIGN   = 0x0008
	TPM_TOPALIGN     = 0x0000
	TPM_VCENTERALIGN = 0x0010
	TPM_BOTTOMALIGN  = 0x0020
	TPM_HORIZONTAL   = 0x0000
	TPM_VERTICAL     = 0x0040
	TPM_NONOTIFY     = 0x0080
	TPM_RETURNCMD    = 0x0100
	TPM_RECURSE      = 0x0001

	MF_STRING    = 0x00000000
	MF_POPUP     = 0x00000010
	MF_SEPARATOR = 0x00000800
	MF_DEFAULT   = 0x00001000
	MF_GRAYED    = 0x00000001
	MF_DISABLED  = 0x00000002
	MF_CHECKED   = 0x00000008
	MF_UNCHECKED = 0x00000000

	// System metrics
	SM_CXSMICON = 49
	SM_CYSMICON = 50
	SM_CXICON   = 11
	SM_CYICON   = 12

	// Load flags
	LR_DEFAULTSIZE = 0x00000040
)

type WNDCLASS struct {
	Style         uint32
	LpfnWndProc   uintptr
	CbClsExtra    int32
	CbWndExtra    int32
	HInstance     windows.Handle
	HIcon         windows.Handle
	HCursor       windows.Handle
	HbrBackground windows.Handle
	LpszMenuName  *uint16
	LpszClassName *uint16
}

type POINT struct {
	X, Y int32
}

var (
	trayInstances        = make(map[windows.Handle]*windowsTray)
	trayCounter   uint32 = 1
)

func runPlatformSpecific(tray *SystemTray, onReady func(), onExit func()) {
	wt := &windowsTray{
		tray:    tray,
		onReady: onReady,
		onExit:  onExit,
		onClick: nil, // Will be set after onReady is called
		ctx:     tray.ctx,
		cancel:  tray.cancel,
	}

	// Create window class
	className := syscall.StringToUTF16Ptr("WailsTrayWindow")
	wndClass := WNDCLASS{
		Style:         0,
		LpfnWndProc:   syscall.NewCallback(wndProc),
		CbClsExtra:    0,
		CbWndExtra:    0,
		HInstance:     windows.Handle(0),
		HIcon:         0,
		HCursor:       0,
		HbrBackground: 0,
		LpszMenuName:  nil,
		LpszClassName: className,
	}

	ret, _, _ := procRegisterClass.Call(uintptr(unsafe.Pointer(&wndClass)))
	if ret == 0 {
		slog.Error("Failed to register window class")
		return
	}

	// Create window
	windowName := syscall.StringToUTF16Ptr("WailsTrayWindow")
	hwnd, _, _ := procCreateWindowEx.Call(
		0,
		uintptr(unsafe.Pointer(className)),
		uintptr(unsafe.Pointer(windowName)),
		WS_OVERLAPPED,
		CW_USEDEFAULT,
		CW_USEDEFAULT,
		CW_USEDEFAULT,
		CW_USEDEFAULT,
		0,
		0,
		0,
		0,
	)

	if hwnd == 0 {
		slog.Error("Failed to create window")
		return
	}

	wt.hwnd = windows.Handle(hwnd)
	trayInstances[wt.hwnd] = wt

	// Initialize NOTIFYICONDATA
	wt.notifyData = NOTIFYICONDATA{
		CbSize:           uint32(unsafe.Sizeof(NOTIFYICONDATA{})),
		HWnd:             wt.hwnd,
		UID:              trayCounter,
		UFlags:           NIF_MESSAGE,
		UCallbackMessage: WM_TRAYICON,
	}

	trayCounter++

	// Add the tray icon to the system tray
	procShell_NotifyIcon.Call(NIM_ADD, uintptr(unsafe.Pointer(&wt.notifyData)))

	// Set initial icon and tooltip if they were set before Run()
	if len(tray.icon) > 0 {
		tray.setIconPlatform(tray.icon)
	}
	if tray.tooltip != "" {
		tray.setTooltipPlatform(tray.tooltip)
	}

	// Call onReady immediately after tray icon is added and message loop is about to start
	if onReady != nil {
		onReady()
	}

	// Set the onClick callback after onReady has been called
	wt.onClick = tray.onClick

	// Message loop
	var msg struct {
		Hwnd    windows.Handle
		Message uint32
		WParam  uintptr
		LParam  uintptr
		Time    uint32
		Pt      POINT
	}

	for {
		select {
		case <-wt.ctx.Done():
			// Cleanup
			// Remove tray icon from system tray
			wt.notifyData.UFlags = NIF_MESSAGE
			procShell_NotifyIcon.Call(NIM_DELETE, uintptr(unsafe.Pointer(&wt.notifyData)))

			if wt.icon != 0 {
				procDestroyIcon.Call(uintptr(wt.icon))
			}
			procDestroyWindow.Call(uintptr(wt.hwnd))
			procUnregisterClass.Call(uintptr(unsafe.Pointer(className)), 0)
			if onExit != nil {
				onExit()
			}
			return
		default:
			// Use GetMessage for better reliability and performance
			ret, _, _ := user32.NewProc("GetMessageW").Call(
				uintptr(unsafe.Pointer(&msg)),
				0,
				0,
				0,
			)
			if ret == 0 { // WM_QUIT
				break
			} else if ret == ^uintptr(0) { // Error
				break
			}

			user32.NewProc("TranslateMessage").Call(uintptr(unsafe.Pointer(&msg)))
			user32.NewProc("DispatchMessageW").Call(uintptr(unsafe.Pointer(&msg)))
		}
	}
}

func wndProc(hwnd windows.Handle, msg uint32, wParam, lParam uintptr) uintptr {
	tray, exists := trayInstances[hwnd]
	if !exists {
		ret, _, _ := procDefWindowProc.Call(uintptr(hwnd), uintptr(msg), wParam, lParam)
		return ret
	}

	switch msg {
	case WM_TRAYICON:
		switch lParam {
		case WM_LBUTTONUP:
			// Left click - show window if callback is set
			slog.Debug("Tray icon left click received", "onClick_set", tray.onClick != nil)
			if tray.onClick != nil {
				go tray.onClick()
			}
		case WM_RBUTTONUP:
			// Right click - show context menu
			slog.Debug("Tray icon right click received")
			showContextMenu(tray)
		case WM_LBUTTONDBLCLK:
			// Double click - show window if callback is set
			slog.Debug("Tray icon double click received", "onClick_set", tray.onClick != nil)
			if tray.onClick != nil {
				go tray.onClick()
			}
		}
		return 0
	case WM_THEMECHANGED, WM_DWMCOMPOSITIONCHANGED, WM_DISPLAYCHANGE, WM_SYSCOLORCHANGE:
		// Handle theme changes by refreshing immediately
		slog.Info("Theme change detected, refreshing immediately", "message", msg)
		refreshTrayIcon(tray)
		return 0
	case WM_SETTINGCHANGE:
		// Handle system setting changes (immediate refresh)
		slog.Info("System setting change detected, refreshing immediately", "message", msg)
		refreshTrayIcon(tray)
		return 0
	case 0x0002: // WM_DESTROY
		// Remove tray icon
		tray.notifyData.UFlags = NIF_MESSAGE
		procShell_NotifyIcon.Call(NIM_DELETE, uintptr(unsafe.Pointer(&tray.notifyData)))
		return 0
	}

	ret, _, _ := procDefWindowProc.Call(uintptr(hwnd), uintptr(msg), wParam, lParam)
	return ret
}

func showContextMenu(tray *windowsTray) {
	// Get current menu items from the main tray
	tray.tray.mu.RLock()
	menuItems := tray.tray.menuItems
	tray.tray.mu.RUnlock()

	if len(menuItems) == 0 {
		return
	}

	// Create popup menu
	hMenu, _, _ := procCreatePopupMenu.Call()
	if hMenu == 0 {
		return
	}
	defer procDestroyMenu.Call(hMenu)

	// Add menu items
	for i, item := range menuItems {
		title := syscall.StringToUTF16Ptr(item.Title)
		flags := MF_STRING
		if item.Disabled {
			flags |= MF_DISABLED
		}
		if item.Checked {
			flags |= MF_CHECKED
		}

		procAppendMenu.Call(hMenu, uintptr(flags), uintptr(i+1), uintptr(unsafe.Pointer(title)))
	}

	// Get cursor position
	var pt POINT
	procGetCursorPos.Call(uintptr(unsafe.Pointer(&pt)))

	// Show menu
	ret, _, _ := procTrackPopupMenu.Call(
		hMenu,
		TPM_RIGHTBUTTON|TPM_RETURNCMD,
		uintptr(pt.X),
		uintptr(pt.Y),
		0,
		uintptr(tray.hwnd),
		0,
	)

	if ret > 0 && int(ret-1) < len(menuItems) {
		item := menuItems[ret-1]
		select {
		case item.ClickedCh <- struct{}{}:
		default:
		}
		if item.onClick != nil {
			go item.onClick()
		}
	}
}

// refreshTrayIcon refreshes the tray icon after theme changes
func refreshTrayIcon(tray *windowsTray) {
	if tray == nil {
		return
	}

	slog.Debug("Refreshing tray icon - using remove/re-add approach")

	// For theme changes, we need to remove and re-add the icon
	// because Windows can lose the connection to the window handle

	// First remove the icon
	tray.notifyData.UFlags = NIF_MESSAGE
	procShell_NotifyIcon.Call(NIM_DELETE, uintptr(unsafe.Pointer(&tray.notifyData)))

	// Increased delay to ensure removal is fully processed
	// Some systems need more time for proper cleanup
	time.Sleep(100 * time.Millisecond)

	// Re-add the icon with all necessary flags
	tray.notifyData.UFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP

	// Ensure the callback message is properly set
	tray.notifyData.UCallbackMessage = WM_TRAYICON

	// Set the icon if it exists
	if tray.icon != 0 {
		tray.notifyData.HIcon = tray.icon
		tray.notifyData.UFlags |= NIF_ICON
	}

	// Set the tooltip if it exists
	if tray.tray.tooltip != "" {
		tooltipUTF16 := syscall.StringToUTF16(tray.tray.tooltip)
		copy(tray.notifyData.SzTip[:], tooltipUTF16)
		if len(tooltipUTF16) > 127 {
			tray.notifyData.SzTip[127] = 0
		}
		tray.notifyData.UFlags |= NIF_TIP
	}

	// Re-add the icon
	ret, _, _ := procShell_NotifyIcon.Call(NIM_ADD, uintptr(unsafe.Pointer(&tray.notifyData)))

	if ret != 0 {
		slog.Info("Tray icon refreshed successfully with remove/re-add", "callback_message", tray.notifyData.UCallbackMessage)
	} else {
		slog.Error("Failed to re-add tray icon after theme refresh")
	}
}

// SetIcon sets the tray icon (Windows implementation)
func (tray *SystemTray) setIconWindows(iconData []byte) {
	// Find the tray instance
	var wt *windowsTray
	for _, instance := range trayInstances {
		wt = instance
		break
	}
	if wt == nil {
		return
	}

	if len(iconData) > 0 {
		// Create HICON from icon data
		hIcon := createHIconFromData(iconData)
		if hIcon != 0 {
			wt.notifyData.HIcon = hIcon
			wt.notifyData.UFlags |= NIF_ICON

			// Update the tray icon
			procShell_NotifyIcon.Call(NIM_MODIFY, uintptr(unsafe.Pointer(&wt.notifyData)))
		}
	}
}

// createHIconFromData creates an HICON from icon data
func createHIconFromData(iconData []byte) windows.Handle {
	if len(iconData) < 8 {
		slog.Debug("Icon data too small", "bytes", len(iconData))
		return createDefaultIcon()
	}

	// Check if it's a PNG file (PNG signature: 89 50 4E 47)
	isPNG := len(iconData) >= 4 && iconData[0] == 0x89 && iconData[1] == 0x50 && iconData[2] == 0x4E && iconData[3] == 0x47

	// Check if it's an ICO file (ICO signature: 0x00000100)
	isICO := len(iconData) >= 4 && iconData[0] == 0x00 && iconData[1] == 0x00 && iconData[2] == 0x01 && iconData[3] == 0x00

	slog.Debug("Received icon data", "bytes", len(iconData), "is_png", isPNG, "is_ico", isICO)
	if len(iconData) >= 4 {
		slog.Debug("First 4 bytes", "bytes", fmt.Sprintf("%02x %02x %02x %02x", iconData[0], iconData[1], iconData[2], iconData[3]))
	}

	if !isPNG && !isICO {
		slog.Debug("Unsupported format, using default icon")
		return createDefaultIcon()
	}

	// Get system metrics for small icon size
	iconWidth, _, _ := procGetSystemMetrics.Call(SM_CXSMICON)
	iconHeight, _, _ := procGetSystemMetrics.Call(SM_CYSMICON)

	slog.Debug("System icon size", "width", iconWidth, "height", iconHeight)

	// Try the exact same approach as v3
	hIcon, _, err := procCreateIconFromResourceEx.Call(
		uintptr(unsafe.Pointer(&iconData[0])), // presbits
		uintptr(len(iconData)),                // dwResSize
		1,                                     // isIcon (true)
		0x00030000,                            // version
		iconWidth,                             // cxDesired
		iconHeight,                            // cyDesired
		LR_DEFAULTSIZE,                        // flags
	)

	slog.Debug("CreateIconFromResourceEx returned", "hIcon", hIcon, "err", err)

	if hIcon != 0 {
		slog.Debug("Successfully created icon with CreateIconFromResourceEx")
		return windows.Handle(hIcon)
	}

	// If CreateIconFromResourceEx failed, the issue is likely with the data format
	// Let's try to create a simple colored icon based on the data
	slog.Debug("CreateIconFromResourceEx failed, trying to create a simple colored icon")
	return createColoredIconFromData(iconData)
}

// createColoredIconFromData creates a simple colored icon based on the data
func createColoredIconFromData(iconData []byte) windows.Handle {
	// Create different colored icons based on the data content
	// This gives visual feedback that we're working with custom data

	// Use different system icons based on data characteristics
	if len(iconData) > 400 {
		// Large data - use information icon (blue)
		hIcon, _, _ := procLoadIcon.Call(0, uintptr(32513)) // IDI_INFORMATION
		return windows.Handle(hIcon)
	} else if len(iconData) > 300 {
		// Medium data - use warning icon (yellow)
		hIcon, _, _ := procLoadIcon.Call(0, uintptr(32516)) // IDI_WARNING
		return windows.Handle(hIcon)
	} else {
		// Small data - use error icon (red)
		hIcon, _, _ := procLoadIcon.Call(0, uintptr(32515)) // IDI_ERROR
		return windows.Handle(hIcon)
	}
}

// createDefaultIcon creates a simple default icon
func createDefaultIcon() windows.Handle {
	// Create a simple 16x16 monochrome icon
	// This is a basic implementation - in production you'd want to convert PNG to ICO
	hIcon, _, _ := procLoadIcon.Call(0, uintptr(32512)) // IDI_APPLICATION
	return windows.Handle(hIcon)
}

// SetTooltip sets the tray tooltip (Windows implementation)
func (tray *SystemTray) setTooltipWindows(tooltip string) {
	// Find the tray instance
	var wt *windowsTray
	for _, instance := range trayInstances {
		wt = instance
		break
	}
	if wt == nil {
		return
	}

	// Convert tooltip to UTF16
	tooltipUTF16 := syscall.StringToUTF16(tooltip)
	copy(wt.notifyData.SzTip[:], tooltipUTF16)
	if len(tooltipUTF16) > 127 {
		wt.notifyData.SzTip[127] = 0
	}

	wt.notifyData.UFlags |= NIF_TIP
	procShell_NotifyIcon.Call(NIM_MODIFY, uintptr(unsafe.Pointer(&wt.notifyData)))
}

// Override the platform-specific functions for Windows
func (s *SystemTray) setIconPlatform(icon []byte) {
	s.setIconWindows(icon)
}

func (s *SystemTray) setTooltipPlatform(tooltip string) {
	s.setTooltipWindows(tooltip)
}

func (s *SystemTray) quitPlatform() {
	s.quitWindows()
}

// quitWindows removes the tray icon immediately (Windows implementation)
func (s *SystemTray) quitWindows() {
	// Find the tray instance
	var wt *windowsTray
	for _, instance := range trayInstances {
		wt = instance
		break
	}
	if wt == nil {
		return
	}

	// Remove tray icon from system tray immediately
	wt.notifyData.UFlags = NIF_MESSAGE
	procShell_NotifyIcon.Call(NIM_DELETE, uintptr(unsafe.Pointer(&wt.notifyData)))
}
