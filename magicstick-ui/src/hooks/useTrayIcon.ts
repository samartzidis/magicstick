import { useEffect, useRef } from "react";
import { TrayIconManager } from "../lib/TrayIconManager";
import type { MagicStickDeviceDetails, KeyboardStatus } from "../types";

/**
 * Reactively manages the system tray icon based on device state.
 *
 * Uses a tristate keyboard status to distinguish between:
 * - "unknown": device just opened, keyboard status not yet reported -> connected icon
 * - "connected": keyboard confirmed connected -> battery icon (if available) or connected icon
 * - "disconnected": keyboard explicitly disconnected -> missing icon
 */
export function useTrayIcon(
  selectedSerial: string,
  isDeviceOpened: boolean,
  keyboardStatus: KeyboardStatus,
  batteryDetails: MagicStickDeviceDetails | null,
) {
  const managerRef = useRef<TrayIconManager | null>(null);

  // Initialize TrayIconManager on mount
  useEffect(() => {
    managerRef.current = new TrayIconManager();
    managerRef.current.updateMissingIcon();
  }, []);

  // Reactively update tray icon whenever device state changes
  useEffect(() => {
    const mgr = managerRef.current;
    if (!mgr) return;

    if (!selectedSerial || !isDeviceOpened) {
      // No device selected or dongle disconnected
      mgr.updateMissingIcon();
    } else if (keyboardStatus === "disconnected") {
      // Keyboard explicitly reported as disconnected
      mgr.updateMissingIcon();
    } else if (batteryDetails && batteryDetails.batteryLevel != null) {
      // Keyboard connected with battery data
      mgr.updateBatteryIcon(batteryDetails);
    } else {
      // Device opened, keyboard status unknown or connected but no battery yet
      mgr.updateConnectedIcon();
    }
  }, [selectedSerial, isDeviceOpened, keyboardStatus, batteryDetails]);
}
