import { getCurrentWindow } from "@tauri-apps/api/window";
import * as AppService from "./bindings/AppService";

/**
 * Window chrome that isn't Wry.NET's anymore: shown by Tauri's own window API, decided by a
 * setting only .NET can read (the "start minimized" registry flag), and a tray-app UX touch
 * (minimizing hides to the tray instead of the taskbar) that has no C# involvement at all.
 *
 * The window starts hidden ("visible": false in tauri.conf.json) so there's no white flash while
 * the frontend boots; this runs once, right away, to decide whether to reveal it.
 */
async function revealWindowUnlessStartMinimized() {
  const window = getCurrentWindow();
  try {
    const startMinimized = await AppService.GetStartMinimized();
    if (!startMinimized) {
      await window.show();
      await window.setFocus();
    }
  } catch {
    // If the backend call fails for any reason, still show the window rather than leaving
    // the user with no way to reach the app.
    await window.show();
  }
}

/**
 * Windows-only tray-app behavior: minimizing hides the window (reachable again from the tray
 * icon) instead of showing it in the taskbar, mirroring the original Wry.NET app.
 */
async function hideInsteadOfMinimize() {
  if (!(await AppService.IsWindows())) return;

  const window = getCurrentWindow();
  await window.onResized(async () => {
    if (await window.isMinimized()) {
      await window.hide();
    }
  });
}

void revealWindowUnlessStartMinimized();
void hideInsteadOfMinimize();
