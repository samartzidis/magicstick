using System;
using System.Linq;
using HidLibrary;
using Microsoft.Extensions.Logging;
using Semver;

namespace MagicStickUI;

public class DeviceEndpointStateChangedEventArgs : EventArgs
{
    public bool Connected { get; }
    public DeviceEndpointStateChangedEventArgs(bool state) { Connected = state; }
}

public class Device : IDisposable
{
    public event EventHandler<DeviceEndpointStateChangedEventArgs> ChargerDeviceEndpointStateChanged = delegate { };
    public event EventHandler<DeviceEndpointStateChangedEventArgs> RpcDeviceEndpointStateChanged = delegate { };
    public event EventHandler<DeviceEndpointStateChangedEventArgs> RequestReportByIdEndpointStateChanged = delegate { };
    public event EventHandler<RpcEventArgs> RpcEventReceived = delegate { };

    #region PresentationModelProperties        
    public string DeviceId { get; }
    public string DeviceSerialNumber { get; }
    public ushort DeviceVersion { get; private set; }                     
    public string DeviceName { get; private set; }
    public string FirmwareId { get; private set; }
    public SemVersion FirmwareSemVer { get; private set; }
    //public bool IsSupportedDevice { get; private set; }
    public bool Connected => ChargerDeviceEndpoint.IsConnected;
    #endregion

    public HidDevice ChargerDeviceEndpoint { get; }
    public HidDevice RpcDeviceEndpoint { get; }
    public HidDevice RequestReportByIdDeviceEndpoint { get; }
    public DeviceRpc Rpc { get; }

    public Device(ILoggerFactory logger, string serial, HidDevice[] hidEndpoints)
    {
        ChargerDeviceEndpoint = hidEndpoints.First(t =>
            (ushort)t.Capabilities.UsagePage == Constants.UsagePageVendorDefined && (ushort)t.Capabilities.Usage == Constants.UsageCharger);
        RpcDeviceEndpoint = hidEndpoints.First(t =>
            (ushort)t.Capabilities.UsagePage == Constants.UsagePageVendorDefined && (ushort)t.Capabilities.Usage == Constants.UsageHidIo);
        RequestReportByIdDeviceEndpoint = hidEndpoints.First(t =>
            (ushort)t.Capabilities.UsagePage == Constants.UsagePageVendorDefined && (ushort)t.Capabilities.Usage == Constants.UsageRequestReportById);

        DeviceSerialNumber = serial;
        DeviceId = serial;
        UpdateDeviceDetails();

        RpcDeviceEndpoint.MonitorDeviceEvents = true;
        ChargerDeviceEndpoint.MonitorDeviceEvents = true;
        RequestReportByIdDeviceEndpoint.MonitorDeviceEvents = true;

        RpcDeviceEndpoint.Removed += () => RpcDeviceEndpointStateChanged(this, new(false));
        RpcDeviceEndpoint.Inserted += () => RpcDeviceEndpointStateChanged(this, new(true));  
            
        ChargerDeviceEndpoint.Removed += () => ChargerDeviceEndpointStateChanged(this, new(false));
        ChargerDeviceEndpoint.Inserted += () => ChargerDeviceEndpointStateChanged(this, new(true));

        RequestReportByIdDeviceEndpoint.Removed += () => RequestReportByIdEndpointStateChanged(this, new(false));
        RequestReportByIdDeviceEndpoint.Inserted += () => RequestReportByIdEndpointStateChanged(this, new(true));

        Rpc = new DeviceRpc(logger, RpcDeviceEndpoint);
        Rpc.RpcEventReceived += (s, e) => RpcEventReceived(this, e);
        Rpc.Start();    
    }

    public void UpdateDeviceDetails()
    {
        ChargerDeviceEndpoint.ReadProduct(out var product);
        DeviceName = Util.GetHidString(product);
        DeviceVersion = (ushort)ChargerDeviceEndpoint.Attributes.Version;
        var semVerInfo = Util.GetSemVerFromDeviceName(DeviceName);
        FirmwareId = semVerInfo.Item1;
        FirmwareSemVer = semVerInfo.Item2;        
    }

    public void Dispose()
    {
        Rpc?.Dispose();
        ChargerDeviceEndpoint?.Dispose();
        RpcDeviceEndpoint?.Dispose();            
    }
}