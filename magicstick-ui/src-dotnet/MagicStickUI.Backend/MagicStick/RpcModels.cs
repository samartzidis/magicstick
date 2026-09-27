using System.Text.Json.Serialization;

namespace MagicStickUI.Backend.MagicStick;

/// <summary>
/// Header byte for the HID I/O chunked protocol.
/// Bits 0-5: payload length (0-63), Bit 6: MoreData, Bit 7: Reserved.
/// </summary>
public struct HidIoHdr
{
    public byte Value { get; set; }

    public byte Length
    {
        get => (byte)(Value & 0x3F);
        set => Value = (byte)((Value & 0xC0) | (value & 0x3F));
    }

    public bool MoreData
    {
        get => (Value & 0x40) != 0;
        set => Value = value ? (byte)(Value | 0x40) : (byte)(Value & ~0x40);
    }

    public bool Reserved
    {
        get => (Value & 0x80) != 0;
        set => Value = value ? (byte)(Value | 0x80) : (byte)(Value & ~0x80);
    }
}

// Base types

public abstract class RpcRequest
{
    [JsonPropertyName("method_name")]
    public abstract string MethodName { get; }

    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();
}

public class RpcReply
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
}

public class RpcEvent
{
    [JsonPropertyName("event_name")]
    public string EventName { get; set; }
}

// Event args / exception

public class RpcEventArgs : EventArgs
{
    public string Name { get; }
    public string Payload { get; }

    public RpcEventArgs(string name, string payload)
    {
        Name = name;
        Payload = payload;
    }
}

public class RpcException : Exception
{
    public RpcException() { }
    public RpcException(string message) : base(message) { }
    public RpcException(string message, Exception innerException) : base(message, innerException) { }
}

// Concrete event types

public class SendUnicodeCharEvent : RpcEvent
{
    [JsonPropertyName("key_code")]
    public int KeyCode { get; set; }
}

// Settings

public sealed class GetSettingsRequest : RpcRequest
{
    [JsonPropertyName("method_name")]
    public override string MethodName => "get_settings";
}

public sealed class GetSettingsReply : RpcReply
{
    [JsonPropertyName("swap_fn_ctrl")]
    public bool SwapFnCtrl { get; set; }

    [JsonPropertyName("swap_alt_cmd")]
    public bool SwapAltCmd { get; set; }

    [JsonPropertyName("bluetooth_disabled")]
    public bool BluetoothDisabled { get; set; }

    [JsonPropertyName("io_timing")]
    public uint IoTiming { get; set; }
}

public sealed class SetSettingsRequest : RpcRequest
{
    [JsonPropertyName("method_name")]
    public override string MethodName => "set_settings";

    [JsonPropertyName("swap_fn_ctrl")]
    public bool SwapFnCtrl { get; set; }

    [JsonPropertyName("swap_alt_cmd")]
    public bool SwapAltCmd { get; set; }

    [JsonPropertyName("bluetooth_disabled")]
    public bool BluetoothDisabled { get; set; }

    [JsonPropertyName("io_timing")]
    public uint IoTiming { get; set; }
}

// Keymap

public sealed class GetKeymapRequest : RpcRequest
{
    [JsonPropertyName("method_name")]
    public override string MethodName => "get_keymap";

    [JsonPropertyName("defaults")]
    public bool Defaults { get; set; }
}

public sealed class GetKeymapReply : RpcReply
{
    [JsonPropertyName("items")]
    public List<string> Items { get; set; }
}

public sealed class SetKeymapRequest : RpcRequest
{
    [JsonPropertyName("method_name")]
    public override string MethodName => "set_keymap";

    [JsonPropertyName("items")]
    public List<string> Items { get; set; }
}

public sealed class SetKeymapReply : RpcReply
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("error")]
    public string Error { get; set; }
}

// Save config

public sealed class SaveConfigRequest : RpcRequest
{
    [JsonPropertyName("method_name")]
    public override string MethodName => "save_config";
}
