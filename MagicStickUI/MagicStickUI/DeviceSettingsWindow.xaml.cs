using HidLibrary;
using System.Windows;
using Microsoft.Extensions.Logging;

namespace MagicStickUI
{
    public partial class DeviceSettingsWindow : Window
    {
        public PresentationDevice Device { get; set; }
        public bool SwapFnCtrl { get; set; }
        public bool SwapAltCmd { get; set; }
        public bool BluetoothDisabled { get; set; }

        private readonly ILogger _logger;
        private readonly ILoggerFactory _loggerFactory;

        public DeviceSettingsWindow(ILogger<DeviceSettingsWindow> logger, ILoggerFactory loggerFactory, PresentationDevice device)
        {
            InitializeComponent();

            _logger = logger;
            _loggerFactory = loggerFactory;
            DataContext = this;
            Device = device;

            var hd = HidDevices.GetDevice(Device.ControlDevicePathEndpoint);
            if (hd is { IsConnected: true } && UpdateViewFromDevice())
                return;
            else
            {
                MessageBox.Show("Failed to read device status", Constants.AppName);
                Close();
            }
        }

        private bool UpdateViewFromDevice()
        {
            var ctrl = new DeviceCtrl(_loggerFactory.CreateLogger<DeviceCtrl>(), Device);

            if (!ctrl.GetConfig(out var swapFnCtrl, out var swapAltCmd, out var bluetoothDisabled))
                return false;

            SwapFnCtrl = swapFnCtrl;
            SwapAltCmd = swapAltCmd;
            BluetoothDisabled = bluetoothDisabled;

            return true;
        }

        public void Save()
        {
            var ctrl = new DeviceCtrl(_loggerFactory.CreateLogger<DeviceCtrl>(), Device);

            if (!ctrl.SetConfig(SwapFnCtrl, SwapAltCmd, BluetoothDisabled))
                MessageBox.Show("Failed to update device", Constants.AppName);

            Close();
        }

        private void Button_Click_Set(object sender, RoutedEventArgs e)
        {
            Save();
        }

        private void Button_Click_Cancel(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
