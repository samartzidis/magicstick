using System;
using System.ComponentModel;
using PropertyChanged;

namespace MagicStickUI
{
    public class PresentationDevice : INotifyPropertyChanged
    {
        private const double MinUpdatePeriodSec = 5 * 60;

        public event PropertyChangedEventHandler? PropertyChanged;
        public void InvokePropertyChanged(object sender, PropertyChangedEventArgs e) => PropertyChanged?.Invoke(sender, e);

        public bool BatteryStatExpired => DateTime.Now > LastUpdate.AddSeconds(MinUpdatePeriodSec);

        public void UpdateLastUpdateTimestamp() => LastUpdate = DateTime.Now;

        public string? DeviceId { get; set; }

        public ushort DeviceVersion { get; set; }

        public string? ChargerDevicePathEndpoint { get; set; }

        public string? ControlDevicePathEndpoint { get; set; }

        public bool Connected { get; set; }

        public string? DeviceSerialNumber { get; set; }   
        
        public string? DeviceName { get; set; }

        public int BatteryPercentage { get; set; }

        public DateTime LastUpdate { get; private set; } = DateTime.MinValue;
        
        [DependsOn(nameof(DeviceName), nameof(BatteryPercentage), nameof(LastUpdate))]
        public string TooltipString => Connected ? $"{DeviceName}, {BatteryPercentage}%" : $"{DeviceName}, Disconnected";
    }
}
