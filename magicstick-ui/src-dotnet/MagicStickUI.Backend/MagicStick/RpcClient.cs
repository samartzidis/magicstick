using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using HidSharp;
using Microsoft.Extensions.Logging;

namespace MagicStickUI.Backend.MagicStick;

/// <summary>
/// Manages the chunked JSON RPC channel over a HidSharp <see cref="HidStream"/>.
/// </summary>
public class RpcClient : IDisposable
{
    public event EventHandler<RpcEventArgs> RpcEventReceived = delegate { };

    private readonly Dictionary<string, TaskCompletionSource<string>> _rpcCalls = new();
    private readonly HidDevice _rpcDevice;
    private HidStream _stream;
    private List<byte> _receiveBuffer = new();
    private readonly ILogger _logger;
    private CancellationTokenSource _tokenSource;
    private Task _readTask;
    private DateTime _lastDataRead = DateTime.MinValue;
    private bool _disposed;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public RpcClient(ILoggerFactory loggerFactory, HidDevice rpcDevice)
    {
        _rpcDevice = rpcDevice ?? throw new ArgumentNullException(nameof(rpcDevice));
        _logger = loggerFactory.CreateLogger<RpcClient>();
    }

    // Lifecycle

    public void Start()
    {
        if (_tokenSource != null || _readTask != null)
            return;

        // Open the stream for the RPC endpoint
        if (_stream == null)
        {
            if (!_rpcDevice.TryOpen(out var stream))
                throw new InvalidOperationException("Failed to open the RPC HID endpoint.");
            _stream = stream;
        }

        _tokenSource = new CancellationTokenSource();
        _readTask = Task.Run(() => ReadLoop(_tokenSource.Token));

        _logger.LogDebug("RPC channel started on {DevicePath}", _rpcDevice.DevicePath);
    }

    public void Stop()
    {
        _tokenSource?.Cancel();

        try { _stream?.Close(); } catch { /* best-effort */ }

        // Wait for the read task to finish so we don't leave it dangling
        try { _readTask?.Wait(5000); } catch { /* swallow */ }

        // Clean up and reset state so Start() can be called again
        _tokenSource?.Dispose();
        _tokenSource = null;
        _readTask = null;
        _stream?.Dispose();
        _stream = null;

        _logger.LogDebug("RPC channel stopped");
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _logger.LogDebug("RPC channel disposing");

        Stop();
    }

    // Public RPC helpers

    public async Task<GetKeymapReply> GetKeymap(bool defaults = false)
        => await CallAsync<GetKeymapRequest, GetKeymapReply>(new GetKeymapRequest { Defaults = defaults }).ConfigureAwait(false);

    public async Task<SetKeymapReply> SetKeymap(SetKeymapRequest req)
        => await CallAsync<SetKeymapRequest, SetKeymapReply>(req).ConfigureAwait(false);

    public async Task<GetSettingsReply> GetSettings()
        => await CallAsync<GetSettingsRequest, GetSettingsReply>(new GetSettingsRequest()).ConfigureAwait(false);

    public async Task SetSettings(SetSettingsRequest req)
        => await FireAsync(req).ConfigureAwait(false);

    public async Task SaveConfig()
        => await FireAsync(new SaveConfigRequest()).ConfigureAwait(false);

    // Write (chunked send)

    /// <summary>
    /// Sends raw bytes using the HID I/O chunked protocol.
    /// Each chunk: [ReportId][HidIoHdr][payload (up to 32 bytes)].
    /// </summary>
    public bool SendRpcData(byte[] data)
    {
        var chunkPayload = Constants.RpcChunkPayloadSize;
        var numChunks = (data.Length + chunkPayload - 1) / chunkPayload;

        // HidSharp write buffer: byte[0]=ReportId, byte[1]=header, byte[2..33]=payload
        var maxOutput = _rpcDevice.GetMaxOutputReportLength();

        for (var i = 0; i < numChunks; i++)
        {
            var start = i * chunkPayload;
            var end = Math.Min(start + chunkPayload, data.Length);
            var dataLen = end - start;

            var header = new HidIoHdr
            {
                Length = (byte)dataLen,
                MoreData = i < numChunks - 1,
            };

            // Build the HID output report
            var report = new byte[maxOutput];
            report[0] = Constants.ReportIdRpc; // Report ID
            report[1] = header.Value;                     // Header byte
            Array.Copy(data, start, report, 2, dataLen);  // Payload

            try
            {
                _stream.Write(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write RPC chunk {Index}/{Total}", i + 1, numChunks);
                return false;
            }
        }

        return true;
    }

    // Read loop

    private async Task ReadLoop(CancellationToken token)
    {
        _logger.LogDebug("ReadLoop enter ({DevicePath})", _rpcDevice.DevicePath);

        _receiveBuffer.Clear();

        while (!token.IsCancellationRequested)
        {
            try
            {
                // HidStream.Read is blocking; set a timeout so we can check cancellation.
                _stream.ReadTimeout = 1000;

                byte[] buffer;
                try
                {
                    buffer = _stream.Read();
                }
                catch (TimeoutException)
                {
                    continue;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (IOException)
                {
                    // Device likely disconnected
                    break;
                }

                // buffer[0] = Report ID, buffer[1] = header, buffer[2..] = payload
                if (buffer.Length < 2)
                    continue;

                var reportId = buffer[0];
                if (reportId != Constants.ReportIdRpc)
                {
                    _logger.LogDebug("Discarding unknown report id: 0x{ReportId:X2}", reportId);
                    continue;
                }

                _lastDataRead = DateTime.UtcNow;

                var hdrByte = buffer[1];
                var len = hdrByte & 0x3F;
                var moreData = (hdrByte & 0x40) != 0;

                if (len > 0 && buffer.Length >= 2 + len)
                {
                    var payload = new byte[len];
                    Array.Copy(buffer, 2, payload, 0, len);
                    _receiveBuffer.AddRange(payload);
                }

                if (!moreData)
                {
                    OnRpcDataComplete(_receiveBuffer.ToArray());
                    _receiveBuffer.Clear();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RPC read loop");
                _receiveBuffer.Clear();

                try
                {
                    await Task.Delay(1000, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _logger.LogDebug("ReadLoop exit ({DevicePath})", _rpcDevice.DevicePath);
    }

    // Dispatch

    private void OnRpcDataComplete(byte[] data)
    {
        _logger.LogDebug("OnRpcDataComplete ({Length} bytes)", data.Length);

        try
        {
            var json = Encoding.UTF8.GetString(data);
            _logger.LogDebug("RPC rx: {Json}", json);

            var node = JsonNode.Parse(json);
            if (node == null)
                return;

            if (node["event_name"] != null)
            {
                var eventName = node["event_name"]!.GetValue<string>();
                _logger.LogDebug("Received event: name={EventName}", eventName);

                RpcEventReceived?.Invoke(this, new RpcEventArgs(eventName, json));
            }
            else
            {
                var id = node["id"]?.GetValue<string>();
                _logger.LogDebug("Received RPC reply: id={Id}", id);

                if (id != null && _rpcCalls.TryGetValue(id, out var tcs))
                {
                    _rpcCalls.Remove(id);
                    tcs.SetResult(json);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RPC data error");
        }
    }

    // Internal call helpers

    /// <summary>
    /// Fire-and-forget RPC call (no reply expected).
    /// </summary>
    private Task FireAsync<TReq>(TReq req) where TReq : RpcRequest
    {
        var json = JsonSerializer.Serialize(req, JsonOptions);
        if (!SendRpcData(Encoding.UTF8.GetBytes(json)))
            throw new RpcException("Device IO error.");
        return Task.CompletedTask;
    }

    /// <summary>
    /// RPC call that waits for a correlated reply.
    /// </summary>
    private async Task<TReply> CallAsync<TReq, TReply>(TReq req)
        where TReq : RpcRequest
    {
        var tcs = new TaskCompletionSource<string>();
        _rpcCalls[req.Id] = tcs;

        var json = JsonSerializer.Serialize(req, JsonOptions);
        if (!SendRpcData(Encoding.UTF8.GetBytes(json)))
            throw new RpcException("Send error.");

        const int completionWaitSeconds = 3;
        while (true)
        {
            var delayTask = Task.Delay(TimeSpan.FromSeconds(completionWaitSeconds));
            var completedTask = await Task.WhenAny(tcs.Task, delayTask).ConfigureAwait(false);

            if (completedTask == tcs.Task)
            {
                try
                {
                    return JsonSerializer.Deserialize<TReply>(tcs.Task.Result, JsonOptions);
                }
                catch (Exception ex)
                {
                    throw new RpcException("Received invalid payload.", ex);
                }
            }

            // If data was recently read, keep waiting (the response may arrive in the next chunk).
            if (DateTime.UtcNow - _lastDataRead >= TimeSpan.FromSeconds(completionWaitSeconds))
                break;
        }

        _rpcCalls.Remove(req.Id);
        throw new RpcException("The receive operation has timed out.");
    }
}
