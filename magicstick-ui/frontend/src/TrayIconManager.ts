import * as Log from "../bindings/github.com/wailsapp/wails/v3/pkg/services/log/logservice";
import * as AppService from "../bindings/magicstick-ui/appservice";

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
            this.isDarkMode = await AppService.IsDarkMode();
            Log.Debug('[TrayIconManager] Loaded theme from backend:', this.isDarkMode ? 'dark' : 'light');
        } catch (error) {
            Log.Error('[TrayIconManager] Failed to load theme from backend:', error);
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
        Log.Debug('[TrayIconManager] Getting base icon:', this.isDarkMode ? 'dark' : 'light');
        return this.isDarkMode ? "assets/icons/Battery_dark.png" : "assets/icons/Battery.png";
    }

    // Get status overlay icon
    private getStatusIcon(status: number): string | null {
        if (status & BatteryStatusCharging)
            return this.isDarkMode ? "assets/icons/Charging_dark.png" : "assets/icons/Charging.png";
        if (status & BatteryStatusGood)
            return this.isDarkMode ? "assets/icons/Charged_dark.png" : "assets/icons/Charged.png";        
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

            const iconData = await AppService.BlendIconData(iconPaths);
            
            // Update the system tray icon
            await AppService.UpdateTrayIcon(iconData);

            // Update tooltip with battery info
            let statusText = "";
            if (status & BatteryStatusCharging) {
                statusText = " (charging)";
            } else if (status & BatteryStatusGood) {
                statusText = "";
            } else if (status & BatteryStatusDischarging) {
                statusText = " (discharging)";
            }
            const tooltip = `magicstick - ${level}%${statusText}`;
            await AppService.UpdateTrayTooltip(tooltip);

            Log.Debug(`[TrayIconManager] Updated battery icon: ${level}%, status: ${status}`);
        } catch (error) {
            Log.Error('[TrayIconManager] Failed to update battery icon:', error);
        }
    }

    // Update tray icon for missing device
    async updateMissingIcon() {
        try {
            const iconPaths = [
                this.getBaseIcon(),
                this.isDarkMode ? "assets/icons/Missing_dark.png" : "assets/icons/Missing.png"
            ];

            const iconData = await AppService.BlendIconData(iconPaths);
            
            // Update the system tray icon
            await AppService.UpdateTrayIcon(iconData);
            
            // Update tooltip
            await AppService.UpdateTrayTooltip("MagicStick - No device connected");

            Log.Debug('[TrayIconManager] Updated missing device icon');
        } catch (error) {
            Log.Error('[TrayIconManager] Failed to update missing icon:', error);
        }
    }

    // Update tray icon for connected device (no battery data yet)
    async updateConnectedIcon() {
        try {
            const iconPaths = [this.getBaseIcon()];
            const iconData = await AppService.BlendIconData(iconPaths);
            
            // Update the system tray icon
            await AppService.UpdateTrayIcon(iconData);
            
            // Update tooltip
            await AppService.UpdateTrayTooltip("MagicStick - Connected");

            Log.Debug('[TrayIconManager] Updated connected device icon');
        } catch (error) {
            Log.Error('[TrayIconManager] Failed to update connected icon:', error);
        }
    }
}

