import * as AppBackend from "../wailsjs/go/main/App";

// Battery status constants (matching backend)
const BatteryStatusGood = 0x01;  // present/OK
const BatteryStatusCharging = 0x02;  // charging
const BatteryStatusDischarging = 0x04;  // discharging

// Tray icon manager class
export class TrayIconManager {
    private isDarkMode: boolean = true;

    constructor() {
        // Get initial theme from backend
        this.loadThemeFromBackend();
    }

    private async loadThemeFromBackend() {
        try {
            // Use backend IsDarkMode method
            this.isDarkMode = await AppBackend.IsDarkMode();
            console.log('[TrayIconManager] Loaded theme from backend:', this.isDarkMode ? 'dark' : 'light');
        } catch (error) {
            console.error('[TrayIconManager] Failed to load theme from backend:', error);
            this.isDarkMode = false;
        }
    }

    // Map battery level to indicator icon
    private getIndicatorIcon(level: number): string {
        if (level >= 100) return "assets/icons/Indicator_100.png";
        if (level >= 90) return "assets/icons/Indicator_90.png";
        if (level >= 80) return "assets/icons/Indicator_80.png";
        if (level >= 70) return "assets/icons/Indicator_70.png";
        if (level >= 60) return "assets/icons/Indicator_60.png";
        if (level >= 50) return "assets/icons/Indicator_50.png";
        if (level >= 40) return "assets/icons/Indicator_40.png";
        if (level >= 30) return "assets/icons/Indicator_30.png";
        if (level >= 20) return "assets/icons/Indicator_20.png";
        if (level >= 10) return "assets/icons/Indicator_10.png";
        return "assets/icons/Indicator_1.png";
    }

    // Get base battery icon based on theme
    private getBaseIcon(): string {
        console.log('[TrayIconManager] Getting base icon:', this.isDarkMode ? 'dark' : 'light');
        return this.isDarkMode ? "assets/icons/Battery_dark.png" : "assets/icons/Battery.png";
    }

    // Get status overlay icon
    private getStatusIcon(status: number): string | null {
        // Check for charging status (bitwise AND)
        if (status & BatteryStatusCharging) {
            return this.isDarkMode ? "assets/icons/Charging_dark.png" : "assets/icons/Charging.png";
        }
        // If not charging but battery is good, use base battery icon
        if (status & BatteryStatusGood) {
            return this.isDarkMode ? "assets/icons/Battery_dark.png" : "assets/icons/Battery.png";
        }
        return null;
    }

    // Update tray icon with battery data
    async updateBatteryIcon(level: number, status: number) {
        try {
            const iconPaths = [this.getBaseIcon(), this.getIndicatorIcon(level)];

            const statusIcon = this.getStatusIcon(status);
            if (statusIcon) {
                iconPaths.push(statusIcon);
            }

            const iconData = await AppBackend.BlendIconData(iconPaths);
            await AppBackend.SetTrayIcon(iconData);

            // Update tooltip
            let statusText = "";
            if (status & BatteryStatusCharging) {
                statusText = " (Charging)";
            } else if (status & BatteryStatusGood) {
                statusText = " (Good)";
            } else if (status & BatteryStatusDischarging) {
                statusText = " (Discharging)";
            }
            const tooltip = `Battery Level: ${level}%${statusText}`;
            await AppBackend.SetTrayTooltip(tooltip);

            console.log(`[TrayIconManager] Updated battery icon: ${level}%, status: ${status}`);
        } catch (error) {
            console.error('[TrayIconManager] Failed to update battery icon:', error);
        }
    }

    // Update tray icon for missing device
    async updateMissingIcon() {
        try {
            const iconPaths = [
                this.getBaseIcon(),
                this.isDarkMode ? "assets/icons/Missing_dark.png" : "assets/icons/Missing.png"
            ];

            const iconData = await AppBackend.BlendIconData(iconPaths);
            await AppBackend.SetTrayIcon(iconData);
            await AppBackend.SetTrayTooltip("MagicStick - Device: Disconnected");

            console.log('[TrayIconManager] Updated missing device icon');
        } catch (error) {
            console.error('[TrayIconManager] Failed to update missing icon:', error);
        }
    }

    // Update tray icon for connected device (no battery data yet)
    async updateConnectedIcon() {
        try {
            const iconPaths = [this.getBaseIcon()];
            const iconData = await AppBackend.BlendIconData(iconPaths);
            await AppBackend.SetTrayIcon(iconData);
            await AppBackend.SetTrayTooltip("Device: Connected");

            console.log('[TrayIconManager] Updated connected device icon');
        } catch (error) {
            console.error('[TrayIconManager] Failed to update connected icon:', error);
        }
    }
}
