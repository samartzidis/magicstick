namespace MagicStickUI.Backend.MagicStick;

public enum BatteryStatus : byte
{
    Unknown = 0,
    Charging = 0x3,
    Discharging = 0x4,
    Charged = 0x5,
}

public readonly record struct BatteryInfo(BatteryStatus Status, int Level);
