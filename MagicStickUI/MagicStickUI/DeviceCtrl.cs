using HidLibrary;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MagicStickUI
{
    public class DeviceCtrl
    {
        [Flags]
        enum HidConfig : byte
        {
            SwapFnCtrl = 0x01,
            SwapAltCmd = 0x02,
            BluetoothDisabled = 0x04
        }

        enum HidControlCommand : byte
        {
            None = 0,
            SetConfig,
            DeleteConfig,
            SaveConfig
        }

        private readonly PresentationDevice _device;
        private readonly ILogger _logger;

        public DeviceCtrl(ILogger<DeviceCtrl> logger, PresentationDevice device)
        {
            _logger = logger;
            _device = device;
        }

        public bool GetConfig(out bool swapFnCtrl, out bool swapAltCmd, out bool bluetoothDisabled)
        {
            _logger.LogDebug("GetConfig");

            swapFnCtrl = false;
            swapAltCmd = false;
            bluetoothDisabled = false;

            var hd = HidDevices.GetDevice(_device.ControlDevicePathEndpoint);
            if (hd is not { IsConnected: true })
                return false;

            try
            {
                hd.OpenDevice();

                var report = hd.ReadReportSync(0x10);
                if (report.ReadStatus == HidDeviceData.ReadStatus.Success)
                {
                    var hidConfigValue = (HidConfig)report.Data[0];

                    swapFnCtrl = hidConfigValue.HasFlag(HidConfig.SwapFnCtrl);
                    swapAltCmd = hidConfigValue.HasFlag(HidConfig.SwapAltCmd);
                    bluetoothDisabled = hidConfigValue.HasFlag(HidConfig.BluetoothDisabled);
                }
                else
                {
                    return false;
                }
            }
            finally
            {
                hd.CloseDevice();
            }

            return true;
        }

        public bool SetConfig(bool swapFnCtrl, bool swapAltCmd, bool bluetoothDisabled)
        {
            _logger.LogDebug("SetConfig");

            var hd = HidDevices.GetDevice(_device.ControlDevicePathEndpoint);
            if (hd is not { IsConnected: true })
                return false;

            try
            {
                hd.OpenDevice();

                var report = new HidReport(10);

                report.ReportId = 0x11;
                report.Data = new byte[10];
                report.Data[0] = (byte)HidControlCommand.SetConfig;
                report.Data[1] = (byte)((swapFnCtrl ? HidConfig.SwapFnCtrl : 0) | (swapAltCmd ? HidConfig.SwapAltCmd : 0) | (bluetoothDisabled ? HidConfig.BluetoothDisabled : 0));
                if (!hd.WriteReport(report))
                    return false;

                report.ReportId = 0x11;
                report.Data = new byte[10];
                report.Data[0] = (byte)HidControlCommand.SaveConfig;
                if (!hd.WriteReport(report))
                    return false;                    
            }
            finally
            {
                hd.CloseDevice();
            }

            return true;
        }

        public bool TryHidReadBattery(out byte value)
        {
            _logger.LogDebug("TryHidReadBattery");

            value = 0;

            var hd = HidDevices.GetDevice(_device.ChargerDevicePathEndpoint);
            if (hd is not { IsConnected: true })
                return false;

            try
            {
                hd.OpenDevice();

                var rep = hd.ReadReportSync(0x90);
                if (rep.ReadStatus == HidDeviceData.ReadStatus.Success)
                {
                    if (rep.Data[0] == 0)
                    {
                        byte? v = null;
                        var task = Task.Run(() =>
                        {
                            var data = hd.Read();
                            if (data.Status == HidDeviceData.ReadStatus.Success && data.Data[0] == 0x90)
                                v = data.Data[2];
                        });

                        if (task.Wait(1000) && v.HasValue)
                        {
                            value = v.Value;
                            return true;
                        }
                    }
                    else
                    {
                        value = rep.Data[1];
                        return true;
                    }
                }
            }
            catch (Exception m)
            {
                Console.WriteLine(m);
            }
            finally
            {
                hd.CloseDevice();
                hd.Dispose();
            }

            return false;
        }
    }
}
