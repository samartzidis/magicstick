use tauri::menu::{Menu, MenuItem};
use tauri::tray::{MouseButton, MouseButtonState, TrayIconBuilder, TrayIconEvent};
use tauri::{Manager, WebviewWindow};
use tauri_plugin_autostart::MacosLauncher;

/// Tauri's own id for the tray icon; the frontend looks it up with this same id
/// (`TrayIcon.getById`) to push dynamically blended icons (see src/lib/TrayIconManager.ts).
const TRAY_ICON_ID: &str = "main-tray";

/// Left-click on the tray icon: mirrors the original Wry.NET app's `ToggleWindow` - hide when
/// visible, otherwise show, restore from minimized, focus, and bounce `always_on_top` to force
/// the window above others (Windows does not otherwise bring a background window to the front).
fn toggle_window(window: &WebviewWindow) {
  let visible = window.is_visible().unwrap_or(false);
  if visible {
    let _ = window.hide();
  } else {
    let _ = window.show();
    let _ = window.unminimize();
    let _ = window.set_focus();
    let _ = window.set_always_on_top(true);
    let _ = window.set_always_on_top(false);
  }
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
  tauri::Builder::default()
    // Must be the first plugin registered (see the plugin's own docs) so a second launch is
    // caught before anything else runs. Focuses the existing window instead of opening a second one.
    .plugin(tauri_plugin_single_instance::init(|app, _args, _cwd| {
      if let Some(window) = app.get_webview_window("main") {
        let _ = window.show();
        let _ = window.unminimize();
        let _ = window.set_focus();
      }
    }))
    .plugin(tauri_plugin_autostart::init(MacosLauncher::LaunchAgent, None))
    // Opens a URL in the system's default browser instead of navigating the webview itself (see
    // AboutPage.tsx's project link).
    .plugin(tauri_plugin_opener::init())
    // Loads the backend from files, or, with the `embedded-backend` feature, from the bundle
    // embedded at compile time (see the crate's `Cargo.toml` for how that bundle is built). In a
    // debug build this runs the backend as a dev-only sidecar process, so a C# rebuild restarts
    // just that process instead of relaunching the whole app.
    .plugin(tauri_plugin_dotnet::init_with(|app| {
      tauri_plugin_dotnet::any_backend_host!(app, "MagicStickUI.Backend")
    }))
    .setup(|app| {
      // Right-click menu: just Exit, since there is otherwise no way to quit once the window is
      // hidden (minimized-to-tray, or "start minimized") - closing a window you can't see isn't
      // possible. `show_menu_on_left_click(false)` keeps left-click exclusively our own toggle
      // below; the native right-click still opens this menu without going through that handler.
      let quit_item = MenuItem::with_id(app, "quit", "Exit", true, None::<&str>)?;
      let tray_menu = Menu::with_items(app, &[&quit_item])?;

      // Tray icon: created once here (click/menu handling and the static starting icon), then
      // kept updated by the frontend as device/battery state changes
      // (TrayIcon.getById("main-tray"), see src/lib/TrayIconManager.ts). The icon bytes for those
      // updates are blended by AppService.BlendIconData in the .NET backend; this plugin has no
      // window/tray service of its own, by design (Tauri's own APIs replace it).
      let mut tray = TrayIconBuilder::with_id(TRAY_ICON_ID)
        .tooltip("MagicStickUI")
        .menu(&tray_menu)
        .show_menu_on_left_click(false)
        .on_menu_event(|app, event| {
          if event.id().as_ref() == "quit" {
            app.exit(0);
          }
        })
        .on_tray_icon_event(|tray, event| {
          if let TrayIconEvent::Click { button: MouseButton::Left, button_state: MouseButtonState::Up, .. } = event {
            if let Some(window) = tray.app_handle().get_webview_window("main") {
              toggle_window(&window);
            }
          }
        });
      if let Some(icon) = app.default_window_icon() {
        tray = tray.icon(icon.clone());
      }
      tray.build(app)?;

      Ok(())
    })
    .run(tauri::generate_context!())
    .expect("error while running tauri application");
}
