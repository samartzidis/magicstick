import { getCurrentWindow } from "@tauri-apps/api/window";
import { TrayIcon } from "@tauri-apps/api/tray";
import { Image } from "@tauri-apps/api/image";
import * as AppService from "../bindings/AppService";
import type { MagicStickDeviceDetails } from "../types";

/** Must match the id the tray icon is built with in src-tauri/src/lib.rs. */
const TRAY_ICON_ID = "main-tray";

function base64ToBytes(base64: string): Uint8Array {
  const binary = atob(base64);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
  return bytes;
}

/**
 * Manages the system tray icon from the frontend, mirroring the legacy
 * magicstick-ui TrayIconManager.ts. Determines which icon layers to blend
 * (via the .NET AppService, which owns the embedded PNG assets) and applies
 * the result through Tauri's own tray icon API - there is no C# tray service
 * in this port, .NET only supplies the pixels.
 */
export class TrayIconManager {
  private isDarkMode: boolean = true;

  constructor() {
    this.loadThemeFromWindow();
  }

  private async loadThemeFromWindow() {
    try {
      this.isDarkMode = (await getCurrentWindow().theme()) === "dark";
    } catch {
      this.isDarkMode = true;
    }
  }

  // Icon name helpers (matching legacy thresholds)

  private getIndicatorIcon(level: number): string {
    if (level >= 100) return "Indicator_100.png";
    if (level >= 90) return "Indicator_90.png";
    if (level >= 80) return "Indicator_80.png";
    if (level >= 70) return "Indicator_70.png";
    if (level >= 60) return "Indicator_60.png";
    if (level >= 50) return "Indicator_50.png";
    if (level >= 40) return "Indicator_40.png";
    if (level >= 30) return "Indicator_30.png";
    if (level >= 20) return "Indicator_20.png";
    if (level >= 10) return "Indicator_10.png";
    return "Indicator_1.png";
  }

  private getBaseIcon(): string {
    return this.isDarkMode ? "Battery_dark.png" : "Battery.png";
  }

  private getChargingIcon(): string {
    return this.isDarkMode ? "Charging_dark.png" : "Charging.png";
  }

  private getMissingIcon(): string {
    return this.isDarkMode ? "Missing_dark.png" : "Missing.png";
  }

  private async applyIcon(iconPaths: string[], tooltip: string) {
    const base64 = await AppService.BlendIconData(iconPaths);
    if (!base64) return;

    const tray = await TrayIcon.getById(TRAY_ICON_ID);
    if (!tray) return;

    await tray.setIcon(await Image.fromBytes(base64ToBytes(base64)));
    await tray.setTooltip(tooltip);
  }

  // Public update methods (matching legacy TrayIconManager.ts)

  /**
   * Update tray icon with battery data.
   * Blends: base + indicator + optional charging overlay.
   */
  async updateBatteryIcon(details: MagicStickDeviceDetails) {
    try {
      if (details.batteryLevel == null) return;

      const iconPaths = [this.getBaseIcon(), this.getIndicatorIcon(details.batteryLevel)];

      const isCharging = details.batteryStatus === "Charging";
      const isDischarging = details.batteryStatus === "Discharging";

      if (isCharging) {
        iconPaths.push(this.getChargingIcon());
      }

      const statusSuffix = isCharging
        ? " (charging)"
        : isDischarging
          ? " (discharging)"
          : "";
      await this.applyIcon(iconPaths, `MagicStick - ${details.batteryLevel}%${statusSuffix}`);
    } catch {
      // Best-effort
    }
  }

  /**
   * Update tray icon for missing/disconnected device.
   */
  async updateMissingIcon() {
    try {
      await this.applyIcon([this.getBaseIcon(), this.getMissingIcon()], "MagicStick - No device connected");
    } catch {
      // Best-effort
    }
  }

  /**
   * Update tray icon for connected device (no battery data yet).
   */
  async updateConnectedIcon() {
    try {
      await this.applyIcon([this.getBaseIcon()], "MagicStick - Connected");
    } catch {
      // Best-effort
    }
  }
}
