namespace MagicStickUI
{
    internal class Constants
    {
        public const string AppName = "magicstick-ui";
        public const string MagicStickFirmwareId = "magicstick";
        public const string MagicStickInitFirmwareId = "magicstick-init";

        public const int VendorIdMagicStick = 0x2E8A; // Raspberry Pi VID
        public const int ProductIdMagicStick = 0xC010;

        public const ushort UsagePageVendorDefined = 0xFF00; // Usage Page (Vendor Defined 0xFF00)
        public const ushort UsageCharger = 0x14; // HID report USAGE (Charger)

        public const ushort UsagePageGenericDesktopControl = 0x01;
        public const ushort UsageVendorDefined = 0x00;
    }
}
