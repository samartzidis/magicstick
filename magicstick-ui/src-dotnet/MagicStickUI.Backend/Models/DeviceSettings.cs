namespace MagicStickUI.Backend.Models;

/// <summary>
/// DTO for device settings passed to/from the frontend.
/// </summary>
public class DeviceSettings
{
    public bool SwapFnCtrl { get; set; }
    public bool SwapAltCmd { get; set; }
    public bool BluetoothDisabled { get; set; }
    public int IoTiming { get; set; }
}
