import { BatteryReportEvent } from "../bindings/magicstick-ui/models";

// Battery status bit flags (matching backend HID report format)
const BatteryStatusPresent = 0x01;     // present
const BatteryStatusCharging = 0x02;    // charging
const BatteryStatusDischarging = 0x04; // discharging

export interface BatteryInfo {
    level: number;
    status: number;
    statusText: string;
    isCharging: boolean;
    isDischarging: boolean;
}

/**
 * Parses battery data from a BatteryReportEvent.
 */
export function parseBatteryStatus(event: BatteryReportEvent): BatteryInfo {
    const { status, level } = event;
    const isPresent = (status & BatteryStatusPresent) !== 0;
    const isCharging = (status & BatteryStatusCharging) !== 0;
    const isDischarging = (status & BatteryStatusDischarging) !== 0;

    let statusText: string;
    if (isCharging) {
        statusText = "Charging";
    } else if (isDischarging) {
        statusText = "Discharging";
    } else if (isPresent) {
        statusText = "Idle";
    } else {
        statusText = "";
    }

    return {
        level,
        status,
        statusText,
        isCharging,
        isDischarging,
    };
}

