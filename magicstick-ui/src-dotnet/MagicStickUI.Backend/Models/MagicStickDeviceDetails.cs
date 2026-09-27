using Tauri.Plugin.DotNet;

namespace MagicStickUI.Backend.Models;

/// <summary>
/// Detailed device info sent to the frontend, both as a call result (GetDeviceStatus,
/// SelectDevice) and as the "batteryReport" event payload.
/// </summary>
[BridgeEvent("batteryReport")]
public class MagicStickDeviceDetails
{
    public string Product { get; set; }
    public string Serial { get; set; }
    public string Manufacturer { get; set; }
    public string DongleStatus { get; set; }
    public string KeyboardStatus { get; set; }
    public int? BatteryLevel { get; set; }
    public string BatteryStatus { get; set; }
}
