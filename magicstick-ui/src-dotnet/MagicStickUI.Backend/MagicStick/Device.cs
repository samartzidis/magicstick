using HidSharp;
using Microsoft.Extensions.Logging;

namespace MagicStickUI.Backend.MagicStick;

/// <summary>
/// Represents a single physical MagicStick device composed of three HID endpoints
/// (Charger, RPC, RequestReportById).
/// </summary>
public class Device : IDisposable
{
    public event EventHandler<RpcEventArgs> RpcEventReceived = delegate { };
    public event EventHandler<BatteryInfo> BatteryReportReceived = delegate { };

    /// <summary>
    /// Raised when the device unexpectedly loses communication
    /// (e.g. USB dongle unplugged, I/O error on a background read loop).
    /// Not raised on an explicit <see cref="Close"/> call.
    /// </summary>
    public event EventHandler Disconnected = delegate { };

    // Device metadata
    public string SerialNumber { get; }
    public string ProductName { get; }
    public int ProductVersion { get; }
    public string DevicePath => _rpcDevice.DevicePath;

    // HID endpoints
    private readonly HidDevice _chargerDevice;
    private readonly HidDevice _rpcDevice;
    private readonly HidDevice _requestReportByIdDevice;

    // Open streams
    private HidStream _chargerStream;
    private HidStream _requestReportByIdStream;

    // RPC channel
    public RpcClient Rpc { get; }

    // Charger read loop
    private Thread _chargerReadThread;
    private volatile bool _chargerReadRunning;

    public bool IsOpen { get; private set; }

    private readonly ILogger _logger;
    private bool _disposed;

    public Device(
        ILoggerFactory loggerFactory,
        string serialNumber,
        HidDevice chargerDevice,
        HidDevice rpcDevice,
        HidDevice requestReportByIdDevice)
    {
        _logger = loggerFactory.CreateLogger<Device>();

        SerialNumber = serialNumber ?? throw new ArgumentNullException(nameof(serialNumber));
        _chargerDevice = chargerDevice ?? throw new ArgumentNullException(nameof(chargerDevice));
        _rpcDevice = rpcDevice ?? throw new ArgumentNullException(nameof(rpcDevice));
        _requestReportByIdDevice = requestReportByIdDevice ?? throw new ArgumentNullException(nameof(requestReportByIdDevice));

        // Read metadata from the first available endpoint
        ProductName = SafeGetProductName(_chargerDevice) ?? SafeGetProductName(_rpcDevice) ?? "MagicStick";
        ProductVersion = _chargerDevice.ReleaseNumberBcd;

        Rpc = new RpcClient(loggerFactory, _rpcDevice);
        Rpc.RpcEventReceived += (_, e) => RpcEventReceived?.Invoke(this, e);
    }

    // Open / Close

    /// <summary>
    /// Opens all three HID streams and starts the RPC read loop.
    /// </summary>
    public void Open()
    {
        if (IsOpen)
            return;

        // Charger (battery reports)
        if (!_chargerDevice.TryOpen(out _chargerStream))
            throw new InvalidOperationException("Failed to open charger HID endpoint.");

        // RequestReportById (battery request)
        if (!_requestReportByIdDevice.TryOpen(out _requestReportByIdStream))
        {
            _chargerStream?.Dispose();
            throw new InvalidOperationException("Failed to open request-report-by-id HID endpoint.");
        }

        // RPC (opened inside Start())
        Rpc.Start();

        // Charger read loop (battery reports)
        _chargerReadRunning = true;
        _chargerReadThread = new Thread(ChargerReadLoop)
        {
            Name = $"Charger-{SerialNumber}",
            IsBackground = true,
        };
        _chargerReadThread.Start();

        IsOpen = true;
        _logger.LogDebug("Device {Serial} opened", SerialNumber);
    }

    /// <summary>
    /// Stops the RPC channel and closes all streams.
    /// </summary>
    public void Close()
    {
        if (!IsOpen)
            return;

        IsOpen = false;

        _chargerReadRunning = false;

        Rpc.Stop();

        try { _chargerStream?.Close(); } catch { /* best-effort */ }
        try { _requestReportByIdStream?.Close(); } catch { /* best-effort */ }

        _chargerReadThread?.Join(2000);
        _chargerReadThread = null;

        _chargerStream = null;
        _requestReportByIdStream = null;

        _logger.LogDebug("Device {Serial} closed", SerialNumber);
    }

    // Battery

    /// <summary>
    /// Sends a request for the charger/battery report via the RequestReportById endpoint.
    /// </summary>
    public bool RequestBatteryReport()
    {
        if (_requestReportByIdStream == null)
            return false;

        try
        {
            var maxOutput = _requestReportByIdDevice.GetMaxOutputReportLength();
            var report = new byte[maxOutput];
            report[0] = Constants.ReportRequestById;   // Report ID
            report[1] = Constants.ReportIdCharger;     // Request charger report

            _requestReportByIdStream.Write(report);
            _logger.LogDebug("Battery report requested");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send battery report request");
            return false;
        }
    }

    /// <summary>
    /// Background loop that continuously reads from the charger stream
    /// and raises <see cref="BatteryReportReceived"/> when a report arrives.
    /// </summary>
    private void ChargerReadLoop()
    {
        _logger.LogDebug("Charger read loop started for {Serial}", SerialNumber);
        var unexpectedExit = false;

        while (_chargerReadRunning)
        {
            try
            {
                if (_chargerStream == null)
                    break;

                _chargerStream.ReadTimeout = 1000;
                var buffer = _chargerStream.Read();

                // buffer[0] = Report ID, buffer[1] = status, buffer[2] = level
                if (buffer.Length < 3)
                    continue;

                var status = (BatteryStatus)buffer[1];
                var level = (int)buffer[2];

                _logger.LogDebug("Battery: {Level}%, status={Status}", level, status);
                BatteryReportReceived?.Invoke(this, new BatteryInfo(status, level));
            }
            catch (TimeoutException)
            {
                // No data within timeout -- loop and try again
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (!_chargerReadRunning) break;
                _logger.LogError(ex, "Charger read error for {Serial}", SerialNumber);
                unexpectedExit = true;
                break;
            }
        }

        _logger.LogDebug("Charger read loop stopped for {Serial}", SerialNumber);

        // If the loop exited due to an I/O error (not a graceful Close), signal disconnection
        if (unexpectedExit && _chargerReadRunning)
        {
            IsOpen = false;
            _logger.LogWarning("Device {Serial} disconnected unexpectedly", SerialNumber);
            Disconnected?.Invoke(this, EventArgs.Empty);
        }
    }

    // Dispose

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        Close();
        Rpc.Dispose();
    }

    // Helpers

    private static string SafeGetProductName(HidDevice device)
    {
        try { return device.GetProductName(); }
        catch { return null; }
    }
}
