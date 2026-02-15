using Microsoft.VisualBasic.ApplicationServices;

namespace MagicStickUI
{
    internal class Constants
    {
        public const string AppName = "magicstick-ui";
        public const string MagicStickFirmwareId = "magicstick";
        public const string MagicStickInitFirmwareId = "magicstick-init";

        public const int VendorIdMagicStick = 0x2E8A;
        public const int ProductIdMagicStick = 0xC010;

        public const ushort UsagePageVendorDefined = 0xFF00; // Usage Page (Vendor Defined 0xFF00)
        public const ushort UsageCharger = 0x14;
        public const ushort UsageHidIo = 0x10;
        public const ushort UsageRequestReportById = 0x11;

        public const byte ReportIdHidIo = 0x12;
        public const byte ReportIdCharger = 0x90;
        public const byte ReportRequestById = 0x91;

        public const byte BatteryStatusDischarging = 0x4;
        public const byte BatteryStatusCharging = 0x3;
        public const byte BatteryStatusCharged = 0x5;
    }
}
