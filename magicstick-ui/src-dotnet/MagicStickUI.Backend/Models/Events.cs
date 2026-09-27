using Tauri.Plugin.DotNet;

namespace MagicStickUI.Backend.Models;

/// <summary>Fired when the device RPC channel reports the keyboard side has connected.</summary>
[BridgeEvent("keyboardConnected")]
public class KeyboardConnectedEvent
{
    public string Serial { get; set; }
}

/// <summary>Fired when the device RPC channel reports the keyboard side has disconnected.</summary>
[BridgeEvent("keyboardDisconnected")]
public class KeyboardDisconnectedEvent
{
    public string Serial { get; set; }
}

/// <summary>Fired when the dongle itself is unplugged or its I/O loop dies.</summary>
[BridgeEvent("deviceDisconnected")]
public class DeviceDisconnectedEvent
{
    public string Serial { get; set; }
}
