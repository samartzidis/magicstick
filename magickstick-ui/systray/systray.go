package systray

import (
	"context"
	"sync"
)

// MenuItem represents a system tray menu item
type MenuItem struct {
	Title     string
	Tooltip   string
	Disabled  bool
	Checked   bool
	ClickedCh chan struct{}
	onClick   func()
}

// NewMenuItem creates a new menu item
func NewMenuItem(title, tooltip string) *MenuItem {
	return &MenuItem{
		Title:     title,
		Tooltip:   tooltip,
		ClickedCh: make(chan struct{}, 1),
	}
}

// SetOnClick sets the click handler for the menu item
func (m *MenuItem) SetOnClick(handler func()) {
	m.onClick = handler
}

// SystemTray represents the system tray implementation
type SystemTray struct {
	title     string
	tooltip   string
	icon      []byte
	menuItems []*MenuItem
	running   bool
	ctx       context.Context
	cancel    context.CancelFunc
	mu        sync.RWMutex
	onClick   func() // Callback for tray icon clicks
}

var (
	globalTray *SystemTray
	trayOnce   sync.Once
)

// GetGlobalTray returns the global system tray instance
func GetGlobalTray() *SystemTray {
	trayOnce.Do(func() {
		globalTray = &SystemTray{
			menuItems: make([]*MenuItem, 0),
		}
	})
	return globalTray
}

// SetTitle sets the tray title
func (s *SystemTray) SetTitle(title string) {
	s.mu.Lock()
	defer s.mu.Unlock()
	s.title = title
}

// SetTooltip sets the tray tooltip
func (s *SystemTray) SetTooltip(tooltip string) {
	s.mu.Lock()
	defer s.mu.Unlock()
	s.tooltip = tooltip

	// Call platform-specific implementation
	if s.running {
		s.setTooltipPlatform(tooltip)
	}
}

// SetIcon sets the tray icon
func (s *SystemTray) SetIcon(icon []byte) {
	s.mu.Lock()
	defer s.mu.Unlock()
	s.icon = icon

	// Call platform-specific implementation
	if s.running {
		s.setIconPlatform(icon)
	}
}

// AddMenuItem adds a menu item to the tray
func (s *SystemTray) AddMenuItem(title, tooltip string) *MenuItem {
	s.mu.Lock()
	defer s.mu.Unlock()

	item := NewMenuItem(title, tooltip)
	s.menuItems = append(s.menuItems, item)
	return item
}

// SetOnClick sets the callback for tray icon clicks
func (s *SystemTray) SetOnClick(handler func()) {
	s.mu.Lock()
	defer s.mu.Unlock()
	s.onClick = handler
}

// Run starts the system tray
func (s *SystemTray) Run(onReady func(), onExit func()) {
	s.mu.Lock()
	defer s.mu.Unlock()

	if s.running {
		return
	}

	s.ctx, s.cancel = context.WithCancel(context.Background())
	s.running = true

	// Start platform-specific implementation
	go s.runPlatform(onReady, onExit)
}

// Quit stops the system tray
func (s *SystemTray) Quit() {
	s.mu.Lock()
	defer s.mu.Unlock()

	if !s.running {
		return
	}

	if s.cancel != nil {
		s.cancel()
	}
	s.running = false

	// Call platform-specific cleanup
	s.quitPlatform()
}

// runPlatform runs the platform-specific implementation
func (s *SystemTray) runPlatform(onReady func(), onExit func()) {
	// This will be implemented by platform-specific files
	runPlatformSpecific(s, onReady, onExit)
}

// Global functions for backward compatibility
func SetTitle(title string) {
	GetGlobalTray().SetTitle(title)
}

func SetTooltip(tooltip string) {
	GetGlobalTray().SetTooltip(tooltip)
}

func SetIcon(icon []byte) {
	GetGlobalTray().SetIcon(icon)
}

func AddMenuItem(title, tooltip string) *MenuItem {
	return GetGlobalTray().AddMenuItem(title, tooltip)
}

func Run(onReady func(), onExit func()) {
	GetGlobalTray().Run(onReady, onExit)
}

func Quit() {
	GetGlobalTray().Quit()
}

func SetOnClick(handler func()) {
	GetGlobalTray().SetOnClick(handler)
}
