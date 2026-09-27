namespace MagicStickUI.Backend.MagicStick;

internal static class Constants
{
    public const int VendorId = 0x2E8A;
    public const int ProductId = 0xC010;

    public const ushort UsagePageVendorDefined = 0xFF00;
    public const ushort UsageRpc = 0x10;
    public const ushort UsageRequestReportById = 0x11;
    public const ushort UsageCharger = 0x14;

    public const byte ReportIdRpc = 0x12;
    public const byte ReportIdCharger = 0x90;
    public const byte ReportRequestById = 0x91;

    /// <summary>
    /// Payload size per RPC chunk (excluding the header byte and report ID).
    /// </summary>
    public const int RpcChunkPayloadSize = 32;
}
