using HidLibrary;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace MagicStickUI
{
    public class Rpc : IDisposable
    {
        public event EventHandler<RpcEventArgs> RpcEventReceived = delegate { };

        private readonly Dictionary<string, TaskCompletionSource<string>> _rpcCalls = new();
        private readonly HidDevice _rpcDeviceEndpoint;
        private List<byte> _receiveBuffer;
        private readonly ILogger _logger;
        private CancellationTokenSource _tokenSource;
        private Task _readTask;
        private DateTime _lastDataRead = DateTime.MinValue;

        public Rpc(ILoggerFactory loggerFactory, HidDevice rpcDeviceEndpoint)
        {
            _rpcDeviceEndpoint = rpcDeviceEndpoint;
            _logger = loggerFactory.CreateLogger<Rpc>();            
        }

        public void Start()
        {            
            if (_tokenSource != null || _readTask != null)
                return;

            _tokenSource = new CancellationTokenSource();
            _readTask = Task.Run(() => Read(_tokenSource.Token));

            _logger.LogDebug("Rpc channel open");
        }

        public void Stop()
        {
            _tokenSource?.Cancel();
            _rpcDeviceEndpoint?.CloseDevice();            
        }

        public void Dispose()
        {
            _logger.LogDebug("Rpc channel disposing");

            Stop();

            if (_readTask.Wait(5000))
            {

                _tokenSource?.Dispose();
                _readTask?.Dispose();

                _logger.LogDebug("Rpc channel disposed.");
            }
            else
            {
                _logger.LogDebug("Rpc channel disposal, wait time exceeded.");
            }
        }

        #region RpcMethods
        public async Task<GetKeymapReply> GetKeymap(bool defaults = false) => await RpcCall<GetKeymapRequest, GetKeymapReply>(new GetKeymapRequest { defaults = defaults }).ConfigureAwait(false);
        public async Task<SetKeymapReply> SetKeymap(SetKeymapRequest req) => await RpcCall<SetKeymapRequest, SetKeymapReply>(req).ConfigureAwait(false);
        public async Task<GetSettingsReply> GetSettings() => await RpcCall<GetSettingsRequest, GetSettingsReply>(new GetSettingsRequest()).ConfigureAwait(false);
        public async Task SetSettings(SetSettingsRequest req) => await RpcCall(req).ConfigureAwait(false);
        public async Task SaveConfig() => await RpcCall(new SaveConfigRequest()).ConfigureAwait(false);
        #endregion

        #region RpcHelpers             

        private async void Read(CancellationToken token)
        {
            _logger.LogDebug($"Read({_rpcDeviceEndpoint.DevicePath}) enter.");

            _receiveBuffer = new List<byte>();
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var rep = _rpcDeviceEndpoint.ReadReport();
                    if (rep.ReadStatus == HidDeviceData.ReadStatus.Success)
                    {
                        if (rep.ReportId == 0x12)
                        {
                            _lastDataRead = DateTime.Now;

                            var hdr = rep.Data[0];
                            var len = hdr & 0x3F;
                            var moreData = (hdr & 0x40) != 0;

                            var data = new byte[len];
                            Array.Copy(rep.Data, 1, data, 0, len);
                            _receiveBuffer.AddRange(data);

                            var bytesStr = Util.ConvertBytesToString(data);
                            _logger.LogDebug(bytesStr);

                            if (!moreData)
                            {
                                OnRpcDataComplete(_receiveBuffer.ToArray());

                                _receiveBuffer.Clear();
                            }
                        }
                        else
                            _logger.LogDebug($"Discarding unknown report id: 0x{rep.ReportId:x}");
                    }
                    else
                    {
                        try
                        {
                            await Task.Delay(1000, token).ConfigureAwait(false);
                        }
                        catch (TaskCanceledException m)
                        {
                            break;
                        }
                    }
                }
                catch (Exception m)
                {
                    _logger.LogError(m, m.Message);

                    _receiveBuffer.Clear();
                }
            }
           

            _logger.LogDebug($"Read({_rpcDeviceEndpoint.DevicePath}) exit.");
        }

        private async Task RpcCall<TReq>(TReq req) where TReq : RpcRequest
        {
            var strData = JsonConvert.SerializeObject(req, Formatting.None);
            if (!SendRpcData(Encoding.UTF8.GetBytes(strData)))
                throw new RpcException("Device IO error.");
        }

        public void OnRpcDataComplete(byte[] data)
        {
            _logger.LogDebug("OnRpcDataComplete()");

            try
            {
                var str = Encoding.UTF8.GetString(data);
                _logger.LogDebug(str);

                var jobj = JObject.Parse(str);
                if (jobj.ContainsKey("event_name"))
                {
                    var rpcEvent = JsonConvert.DeserializeObject<RpcEvent>(str);
                    _logger.LogDebug($"Received event: name={rpcEvent?.event_name}");
                    
                    if (rpcEvent?.event_name != null)
                        RpcEventReceived?.Invoke(this, new RpcEventArgs(rpcEvent.event_name, str));
                }
                else
                {
                    var rpcReply = JsonConvert.DeserializeObject<RpcReply>(str);
                    _logger.LogDebug($"Received RPC reply: id={rpcReply?.id}");

                    if (rpcReply?.id != null && _rpcCalls.TryGetValue(rpcReply.id, out var tcs))
                    {
                        _rpcCalls.Remove(rpcReply.id);
                        tcs.SetResult(str);
                    }
                }
            }
            catch (Exception m)
            {
                _logger.LogError(m, "Rpc data error");
            }
        }

        public bool SendRpcData(byte[] data)
        {
            var numChunks = (data.Length + 31) / 32;

            for (var i = 0; i < numChunks; ++i)
            {
                var start = i * 32;
                var end = (i + 1) * 32;
                if (end > data.Length)
                    end = data.Length;

                var chunk = new byte[33];
                var dataLen = end - start;
                Array.Copy(data, start, chunk, 1, dataLen);

                // Insert the header byte at the beginning
                var header = new HidIoHdr { Length = (byte)dataLen, MoreData = i < numChunks - 1 };
                chunk[0] = header.Value;

                var rep = new HidReport(chunk.Length) { ReportId = 0x12, Data = chunk };
                if (!_rpcDeviceEndpoint.WriteReport(rep, 5 * 1000))
                    return false;
            }

            return true;
        }

        private async Task<TRep> RpcCall<TReq, TRep>(TReq req) where TReq : RpcRequest
        {
            var tcs = new TaskCompletionSource<string>();
            _rpcCalls[req.id] = tcs;

            var strData = JsonConvert.SerializeObject(req, Formatting.None);
            if (!SendRpcData(Encoding.UTF8.GetBytes(strData)))
                throw new RpcException("Send error.");

            const int completionWaitSeconds = 3;
            do
            {
                var delayTask = Task.Delay(TimeSpan.FromSeconds(completionWaitSeconds));
                var completedTask = await Task.WhenAny(tcs.Task, delayTask).ConfigureAwait(false);
                if (completedTask == tcs.Task)
                {
                    try
                    {
                        return JsonConvert.DeserializeObject<TRep>(tcs.Task.Result);
                    }
                    catch (Exception m)
                    {
                        throw new RpcException("Received invalid payload.", m);
                    }
                }
            } while (DateTime.Now - _lastDataRead < TimeSpan.FromSeconds(completionWaitSeconds)); // If there was some data read within the completion wait period, don't fail with a timeout.


            throw new RpcException("The receive operation has timed out.");
        }

        //public static byte[] Compress(byte[] data)
        //{
        //    using var compressedStream = new MemoryStream();
        //    using (var s = new ZLibStream(compressedStream, CompressionMode.Compress))
        //        s.Write(data, 0, data.Length);
        //    return compressedStream.ToArray();
        //}

        //public static byte[] Decompress(byte[] compressedData)
        //{
        //    using var compressedStream = new MemoryStream(compressedData);
        //    using var s = new ZLibStream(compressedStream, CompressionMode.Decompress);
        //    using var resultStream = new MemoryStream();
        //    s.CopyTo(resultStream);
        //    return resultStream.ToArray();
        //}
        #endregion
    }

    public class RpcEventArgs : EventArgs
    {
        public string Name { get; }
        public string Payload { get; }

        public RpcEventArgs(string name, string payload) { Name = name; Payload = payload; }
    }

    public class RpcException : Exception
    {
        public RpcException()
        {
        }

        public RpcException(string message) : base(message)
        {
        }

        public RpcException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    #region RpcModels
    public struct HidIoHdr
    {
        public byte Value { get; set; } // Byte to represent the entire struct

        public byte Length
        {
            get => (byte)(Value & 0x3F); // Mask with 00111111 to extract the first 6 bits
            set => Value = (byte)((Value & 0xC0) | (value & 0x3F)); // Clear the first 6 bits and set new value
        }

        public bool MoreData
        {
            get => (Value & 0x40) != 0; // Check if the 7th bit is set
            set => Value = value ? (byte)(Value | 0x40) : (byte)(Value & ~0x40); // Set or clear the 7th bit
        }

        public bool Reserved
        {
            get => (Value & 0x80) != 0; // Check if the 8th bit is set
            set => Value = value ? (byte)(Value | 0x80) : (byte)(Value & ~0x80); // Set or clear the 8th bit
        }
    }

    public abstract class RpcRequest
    {
        public abstract string method_name { get; }
        public string id { get; set; } = Guid.NewGuid().ToString();
    }

    public class RpcReply
    {
        public string id { get; set; }
    }

    public class RpcEvent
    {
        public string event_name { get; set; }     
    }

    public class SendUnicodeCharEvent : RpcEvent
    {
        public int key_code { get; set; }
    }

    public sealed class GetSettingsRequest : RpcRequest
    {
        public override string method_name => "get_settings";
    }

    public sealed class GetSettingsReply : RpcReply
    {
        public bool swap_fn_ctrl { get; set; }
        public bool swap_alt_cmd { get; set; }
        public bool bluetooth_disabled { get; set; }
        public uint io_timing { get; set; }
    }

    public sealed class SetSettingsRequest : RpcRequest
    {
        public override string method_name => "set_settings";

        public bool swap_fn_ctrl { get; set; }
        public bool swap_alt_cmd { get; set; }
        public bool bluetooth_disabled { get; set; }
        public uint io_timing { get; set; }
    }

    public sealed class GetKeymapRequest : RpcRequest
    {
        public override string method_name => "get_keymap";
        public bool defaults { get; set; }
    }

    public sealed class GetKeymapReply : RpcReply
    {
        public List<string> items { get; set; }
    }

    public sealed class SetKeymapRequest : RpcRequest
    {
        public override string method_name => "set_keymap";

        public List<string> items { get; set; }
    }

    public sealed class SetKeymapReply : RpcReply
    {
        public bool success { get; set; }
        public string error { get; set; }
    }

    public sealed class SaveConfigRequest : RpcRequest
    {
        public override string method_name => "save_config";
    }

    #endregion
}

