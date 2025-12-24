import * as Log from "../bindings/github.com/wailsapp/wails/v3/pkg/services/log/logservice";
import * as AppService from "../bindings/magicstick-ui/appservice";
import { BatteryReportEvent } from "../bindings/magicstick-ui/models";
import { parseBatteryStatus } from "./batteryStatus";

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

    // Get charging overlay icon (only shown when charging)
    private getChargingIcon(): string {
        return this.isDarkMode ? "assets/icons/Charging_dark.png" : "assets/icons/Charging.png";
    }

    // Update tray icon with battery data
    async updateBatteryIcon(event: BatteryReportEvent) {
        try {
            const battery = parseBatteryStatus(event);
            const iconPaths = [this.getBaseIcon(), this.getIndicatorIcon(battery.level)];

            if (battery.isCharging) {
                iconPaths.push(this.getChargingIcon());
            }

            const iconData = await AppService.BlendIconData(iconPaths);
            
            // Update the system tray icon
            await AppService.UpdateTrayIcon(iconData);

            // Update tooltip with battery info
            const statusSuffix = battery.isCharging ? " (charging)" : 
                                 battery.isDischarging ? " (discharging)" : "";
            const tooltip = `magicstick - ${battery.level}%${statusSuffix}`;
            await AppService.UpdateTrayTooltip(tooltip);

            Log.Debug(`[TrayIconManager] Updated battery icon: ${battery.level}%, status: ${battery.statusText}`);
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

