using HidSharp;
using HidSharp.Reports;
using Microsoft.Extensions.Logging;

namespace MagicStickUI.Backend.MagicStick;

/// <summary>
/// Discovers MagicStick USB devices, tracks connections/disconnections,
/// and manages <see cref="Device"/> instances.
/// </summary>
public class DeviceManager : IDisposable
{
    public event EventHandler<Device> DeviceConnected = delegate { };
    public event EventHandler<Device> DeviceDisconnected = delegate { };

    private readonly Dictionary<string, Device> _devices = new();
    private readonly object _lock = new();
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private bool _running;
    private bool _disposed;

    public DeviceManager(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = loggerFactory.CreateLogger<DeviceManager>();
    }

    /// <summary>
    /// A snapshot of currently known devices keyed by serial number.
    /// </summary>
    public IReadOnlyList<Device> Devices
    {
        get
        {
            lock (_lock)
                return _devices.Values.ToList().AsReadOnly();
        }
    }

    // Start / Stop

    /// <summary>
    /// Begins monitoring for MagicStick devices. Performs an initial scan
    /// and subscribes to hotplug notifications.
    /// </summary>
    public void Start()
    {
        if (_running)
            return;

        _running = true;
        DeviceList.Local.Changed += OnDeviceListChanged;

        ScanDevices();

        _logger.LogDebug("DeviceManager started");
    }

    /// <summary>
    /// Stops monitoring and disposes all tracked devices.
    /// </summary>
    public void Stop()
    {
        if (!_running)
            return;

        _running = false;
        DeviceList.Local.Changed -= OnDeviceListChanged;

        lock (_lock)
        {
            foreach (var device in _devices.Values)
            {
                device.Dispose();
            }
            _devices.Clear();
        }

        _logger.LogDebug("DeviceManager stopped");
    }

    // Scanning

    /// <summary>
    /// Re-enumerates all MagicStick devices, creating new instances for
    /// newly connected devices and disposing instances whose devices have
    /// disappeared.
    /// </summary>
    public void ScanDevices()
    {
        _logger.LogDebug("ScanDevices()");

        try
        {
            var hidDevices = DeviceList.Local.GetHidDevices(
                Constants.VendorId,
                Constants.ProductId);

            // Group endpoints by serial number (one physical device = 3 HID interfaces)
            var grouped = new Dictionary<string, List<HidDevice>>();
            foreach (var hd in hidDevices)
            {
                string serial;
                try
                {
                    serial = hd.GetSerialNumber();
                }
                catch
                {
                    _logger.LogDebug("Skipping device without serial: {Path}", hd.DevicePath);
                    continue;
                }

                if (string.IsNullOrEmpty(serial))
                    continue;

                if (!grouped.ContainsKey(serial))
                    grouped[serial] = new List<HidDevice>();

                grouped[serial].Add(hd);
            }

            lock (_lock)
            {
                // Remove devices that are no longer present
                var removed = _devices.Keys.Except(grouped.Keys).ToList();
                foreach (var serial in removed)
                {
                    if (_devices.TryGetValue(serial, out var dev))
                    {
                        _logger.LogDebug("Device removed: {Serial}", serial);
                        _devices.Remove(serial);
                        dev.Dispose();
                        DeviceDisconnected?.Invoke(this, dev);
                    }
                }

                // Add newly connected devices
                foreach (var kvp in grouped)
                {
                    var serial = kvp.Key;
                    var endpoints = kvp.Value;

                    if (_devices.ContainsKey(serial))
                        continue; // already tracked

                    var device = TryCreateDevice(serial, endpoints);
                    if (device != null)
                    {
                        _devices[serial] = device;
                        _logger.LogDebug("Device added: {Serial} ({Name})", serial, device.ProductName);
                        DeviceConnected?.Invoke(this, device);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during device scan");
        }
    }

    // Hotplug

    private void OnDeviceListChanged(object sender, DeviceListChangedEventArgs e)
    {
        if (!_running)
            return;

        _logger.LogDebug("DeviceList changed, re-scanning");
        ScanDevices();
    }

    // Endpoint matching

    /// <summary>
    /// Attempts to identify the three required endpoints (by usage) and
    /// construct a <see cref="Device"/>.
    /// Returns <c>null</c> if the endpoints cannot be matched.
    /// </summary>
    private Device TryCreateDevice(string serial, List<HidDevice> endpoints)
    {
        HidDevice charger = null;
        HidDevice rpc = null;
        HidDevice requestReportById = null;

        foreach (var ep in endpoints)
        {
            var usage = GetTopLevelUsage(ep);
            if (usage == null)
                continue;

            var (usagePage, usageId) = usage.Value;

            if (usagePage != Constants.UsagePageVendorDefined)
                continue;

            switch (usageId)
            {
                case Constants.UsageCharger:
                    charger = ep;
                    break;
                case Constants.UsageRpc:
                    rpc = ep;
                    break;
                case Constants.UsageRequestReportById:
                    requestReportById = ep;
                    break;
            }
        }

        if (charger == null || rpc == null || requestReportById == null)
        {
            _logger.LogWarning(
                "Device {Serial}: incomplete endpoints (charger={HasCharger}, rpc={HasRpc}, reqReport={HasReqReport})",
                serial, charger != null, rpc != null, requestReportById != null);
            return null;
        }

        try
        {
            return new Device(_loggerFactory, serial, charger, rpc, requestReportById);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Device for {Serial}", serial);
            return null;
        }
    }

    /// <summary>
    /// Extracts the top-level usage page and usage from a HID device's
    /// report descriptor. Returns <c>null</c> if parsing fails.
    /// </summary>
    private (ushort UsagePage, ushort Usage)? GetTopLevelUsage(HidDevice device)
    {
        try
        {
            var descriptor = device.GetReportDescriptor();
            var items = descriptor.DeviceItems;
            if (items == null || items.Count == 0)
                return null;

            // Each top-level collection has usages; the first usage encodes
            // the usage page in the high 16 bits and the usage in the low 16 bits.
            var firstItem = items[0];
            var usages = firstItem.Usages;

            foreach (var usage in usages.GetAllValues())
            {
                var usagePage = (ushort)((uint)usage >> 16);
                var usageId = (ushort)((uint)usage & 0xFFFF);

                if (usagePage == Constants.UsagePageVendorDefined)
                    return (usagePage, usageId);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not parse report descriptor for {Path}", device.DevicePath);
            return null;
        }
    }

    // Dispose

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        Stop();
    }
}
