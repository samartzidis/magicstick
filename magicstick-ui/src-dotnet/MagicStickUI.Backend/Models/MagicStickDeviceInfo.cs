namespace MagicStickUI.Backend.Models;

/// <summary>
/// Summary of a discovered device, as listed by DeviceManagerService.GetDevices.
/// </summary>
public class MagicStickDeviceInfo
{
    public string SerialNumber { get; set; }
    public string ProductName { get; set; }
    public int ProductVersion { get; set; }
}
