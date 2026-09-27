using System.Text.Json;
using Tauri.Plugin.DotNet;
using MagicStickUI.Backend.MagicStick;
using MagicStickUI.Backend.Models;
using MagicStickUI.Backend.Util;

namespace MagicStickUI.Backend.Services;

[BridgeService]
public class DeviceManagerService
{
    private readonly BridgeDispatcher _dispatcher;
    private readonly DeviceManager _deviceManager;
    private Device _openDevice;
    private readonly object _lock = new();

    public DeviceManagerService(BridgeDispatcher dispatcher, DeviceManager deviceManager)
    {
        _dispatcher = dispatcher;
        _deviceManager = deviceManager;

        // Listen for hotplug removal of the currently selected device
        _deviceManager.DeviceDisconnected += OnDeviceManagerDisconnected;
    }

    public void Ping()
    {
    }

    /// <summary>
    /// Returns the list of currently discovered MagicStick devices.
    /// </summary>
    public MagicStickDeviceInfo[] GetDevices()
    {
        return _deviceManager.Devices
            .Select(d => new MagicStickDeviceInfo
            {
                SerialNumber = d.SerialNumber,
                ProductName = d.ProductName,
                ProductVersion = d.ProductVersion,
            })
            .ToArray();
    }

    /// <summary>
    /// Triggers a scan for MagicStick devices. Call GetDevices() to get the updated list.
    /// </summary>
    public void ScanDevices()
    {
        _deviceManager.ScanDevices();
    }

    /// <summary>
    /// Selects and opens a device by serial number. Closes any previously opened device.
    /// Returns the initial device details (without battery -- that arrives via event).
    /// Returns null if the device was not found; throws if it was found but could not be opened.
    /// </summary>
    public MagicStickDeviceDetails SelectDevice(string serialNumber)
    {
        lock (_lock)
        {
            CloseOpenDevice();

            if (string.IsNullOrEmpty(serialNumber))
                return null;

            var device = _deviceManager.Devices.FirstOrDefault(d => d.SerialNumber == serialNumber);
            if (device == null)
                return null;

            try
            {
                device.BatteryReportReceived += OnBatteryReport;
                device.Disconnected += OnDeviceDisconnected;
                device.RpcEventReceived += OnRpcEvent;
                device.Open();
                _openDevice = device;
            }
            catch
            {
                device.BatteryReportReceived -= OnBatteryReport;
                device.Disconnected -= OnDeviceDisconnected;
                device.RpcEventReceived -= OnRpcEvent;
                throw;
            }

            return BuildDetails(device, null);
        }
    }

    /// <summary>
    /// Sends a battery report request to the currently selected device.
    /// The response arrives asynchronously as a "batteryReport" event.
    /// </summary>
    public void RequestBatteryReport()
    {
        lock (_lock)
        {
            _openDevice?.RequestBatteryReport();
        }
    }

    /// <summary>
    /// Returns the current device details (without battery -- use events for that).
    /// Returns null if no device is selected/open.
    /// </summary>
    public MagicStickDeviceDetails GetDeviceStatus()
    {
        lock (_lock)
        {
            if (_openDevice == null)
                return null;

            return BuildDetails(_openDevice, null);
        }
    }

    /// <summary>
    /// Reads the current device settings via RPC.
    /// </summary>
    public async Task<DeviceSettings> GetSettings()
    {
        var reply = await RequireOpenDevice().Rpc.GetSettings();
        return new DeviceSettings
        {
            SwapFnCtrl = reply.SwapFnCtrl,
            SwapAltCmd = reply.SwapAltCmd,
            BluetoothDisabled = reply.BluetoothDisabled,
            IoTiming = (int)reply.IoTiming,
        };
    }

    /// <summary>
    /// Applies settings to the device via RPC (volatile -- not persisted until SaveConfig).
    /// </summary>
    public async Task ApplySettings(DeviceSettings settings)
    {
        await RequireOpenDevice().Rpc.SetSettings(new SetSettingsRequest
        {
            SwapFnCtrl = settings.SwapFnCtrl,
            SwapAltCmd = settings.SwapAltCmd,
            BluetoothDisabled = settings.BluetoothDisabled,
            IoTiming = (uint)settings.IoTiming,
        });
    }

    /// <summary>
    /// Persists current settings and keymap to the device's non-volatile memory.
    /// </summary>
    public async Task SaveConfig()
    {
        await RequireOpenDevice().Rpc.SaveConfig();
    }

    /// <summary>
    /// Gets the keymap from the currently selected device.
    /// </summary>
    /// <param name="defaults">If true, returns default keymap; otherwise current keymap.</param>
    public async Task<GetKeymapResult> GetKeymap(bool defaults = false)
    {
        var reply = await RequireOpenDevice().Rpc.GetKeymap(defaults).ConfigureAwait(false);
        return new GetKeymapResult { Items = reply.Items?.ToArray() ?? Array.Empty<string>() };
    }

    /// <summary>
    /// Applies the given keymap lines to the currently selected device.
    /// </summary>
    public async Task<SetKeymapResult> SetKeymap(string[] items)
    {
        var req = new SetKeymapRequest { Items = items?.ToList() ?? new List<string>() };
        var reply = await RequireOpenDevice().Rpc.SetKeymap(req).ConfigureAwait(false);
        return new SetKeymapResult { Success = reply.Success, Error = reply.Error ?? "" };
    }

    // Event handlers

    private void OnBatteryReport(object sender, BatteryInfo battery)
    {
        var device = sender as Device;
        var details = BuildDetails(device, battery);
        _dispatcher.Emit(details);
    }

    /// <summary>
    /// Fired when the device RPC channel receives an event (e.g. keyboard connected/disconnected, send_unicode_char_event).
    /// </summary>
    private void OnRpcEvent(object sender, RpcEventArgs e)
    {
        if (e.Name == "disconnected")
        {
            var device = sender as Device;
            _dispatcher.Emit(new KeyboardDisconnectedEvent { Serial = device?.SerialNumber });
        }
        else if (e.Name == "connected")
        {
            var device = sender as Device;
            _dispatcher.Emit(new KeyboardConnectedEvent { Serial = device?.SerialNumber });
        }
        else if (e.Name == "send_unicode_char_event")
        {
            if (!OperatingSystem.IsWindows())
                return; // Keyboard injection is Windows-only; skip on macOS/Linux

            try
            {
                var evt = JsonSerializer.Deserialize<SendUnicodeCharEvent>(e.Payload);
                if (evt != null)
                    KeyboardInputSender.SendUnicodeToActiveWindow(evt.KeyCode);
            }
            catch
            {
                // Ignore deserialization or SendInput errors
            }
        }
    }

    /// <summary>
    /// Fired when the open device itself detects an I/O error (charger read loop died).
    /// </summary>
    private void OnDeviceDisconnected(object sender, EventArgs e)
    {
        lock (_lock)
        {
            var device = sender as Device;
            if (device != _openDevice) return;

            var serial = device.SerialNumber;
            DetachOpenDevice();

            _dispatcher.Emit(new DeviceDisconnectedEvent { Serial = serial });
        }
    }

    /// <summary>
    /// Fired when the DeviceManager hotplug scan removes a device (USB yanked).
    /// </summary>
    private void OnDeviceManagerDisconnected(object sender, Device device)
    {
        lock (_lock)
        {
            if (device != _openDevice) return;

            var serial = device.SerialNumber;
            DetachOpenDevice();

            _dispatcher.Emit(new DeviceDisconnectedEvent { Serial = serial });
        }
    }

    // Helpers

    private Device RequireOpenDevice()
    {
        lock (_lock)
        {
            return _openDevice ?? throw new InvalidOperationException("No device is open.");
        }
    }

    private void DetachOpenDevice()
    {
        if (_openDevice == null) return;

        _openDevice.BatteryReportReceived -= OnBatteryReport;
        _openDevice.Disconnected -= OnDeviceDisconnected;
        _openDevice.RpcEventReceived -= OnRpcEvent;
        _openDevice = null;
    }

    private void CloseOpenDevice()
    {
        if (_openDevice == null) return;

        _openDevice.BatteryReportReceived -= OnBatteryReport;
        _openDevice.Disconnected -= OnDeviceDisconnected;
        _openDevice.RpcEventReceived -= OnRpcEvent;
        try { _openDevice.Close(); } catch { }
        _openDevice = null;
    }

    private static MagicStickDeviceDetails BuildDetails(Device device, BatteryInfo? battery)
    {
        var details = new MagicStickDeviceDetails
        {
            Product = device.ProductName,
            Serial = device.SerialNumber,
            Manufacturer = "magicstick",
            DongleStatus = device.IsOpen ? "Connected" : "Disconnected",
            KeyboardStatus = battery.HasValue ? "Connected" : "Disconnected",
        };

        if (battery.HasValue)
        {
            details.BatteryLevel = battery.Value.Level;
            details.BatteryStatus = battery.Value.Status switch
            {
                BatteryStatus.Charging => "Charging",
                BatteryStatus.Discharging => "Discharging",
                BatteryStatus.Charged => "Charged",
                _ => "Unknown",
            };
        }

        return details;
    }
}
