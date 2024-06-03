using System;
using System.Threading.Tasks;

namespace HidLibrary
{
    public delegate void InsertedEventHandler();
    public delegate void RemovedEventHandler();

    public enum DeviceMode
    {
        NonOverlapped = 0,
        Overlapped = 1
    }

    [Flags]
    public enum ShareMode
    {
        Exclusive = 0,
        ShareRead = NativeMethods.FILE_SHARE_READ,
        ShareWrite = NativeMethods.FILE_SHARE_WRITE
    }

    public interface IHidDevice : IDisposable
    {
        event InsertedEventHandler Inserted;
        event RemovedEventHandler Removed;

        IntPtr ReadHandle { get; }
        IntPtr WriteHandle { get; }
        bool IsOpen { get; }
        bool IsConnected { get; }
        string Description { get; }
        HidDeviceCapabilities Capabilities { get; }
        HidDeviceAttributes Attributes { get;  }
        string DevicePath { get; }

        bool MonitorDeviceEvents { get; set; }

        void OpenDevice();

        void OpenDevice(DeviceMode readMode, DeviceMode writeMode, ShareMode shareMode);

        void CloseDevice();

        HidDeviceData Read();

        Task<HidDeviceData> ReadAsync(int timeout = 0);

        HidDeviceData Read(int timeout);

        Task<HidReport> ReadReportAsync(int timeout = 0);

        HidReport ReadReport(int timeout);

        HidReport ReadReport();

        bool ReadFeatureData(out byte[] data, byte reportId = 0);

        bool ReadProduct(out byte[] data);

        bool ReadManufacturer(out byte[] data);

        bool ReadSerialNumber(out byte[] data);

        bool Write(byte[] data);

        bool Write(byte[] data, int timeout);

        Task<bool> WriteAsync(byte[] data, int timeout = 0);

        bool WriteReport(HidReport report);

        bool WriteReport(HidReport report, int timeout);

        Task<bool> WriteReportAsync(HidReport report, int timeout = 0);

        HidReport CreateReport();

        bool WriteFeatureData(byte[] data);
    }
}
