using HidLibrary;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using PropertyChanged;
using Semver;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Newtonsoft.Json;

namespace MagicStickUI
{
    [AddINotifyPropertyChangedInterface]
    public partial class MainWindow : Window
    {        
#if DEBUG
        private const int UpdatePollPeriodMsec = 60 * 1000;
#else
        private const int UpdatePollPeriodMsec = 15 * 60 * 1000; // 15 minutes
#endif

        public ObservableCollection<Device> Devices { get; } = new();

        [PropertyChanged.DependsOn(nameof(SelectedDevice))]
        public bool HasConnectedDevice => SelectedDevice is { Connected: true };

        [PropertyChanged.DependsOn(nameof(HasConnectedDevice))]
        public bool HasRealConnectedDevice => HasConnectedDevice && SelectedDevice is { IsSupportedDevice: true } && string.Equals(Constants.MagicStickFirmwareId, SelectedDevice?.FirmwareId, StringComparison.OrdinalIgnoreCase);

        [PropertyChanged.DependsOn(nameof(SelectedDevice))]
        public string TooltipString => SelectedDevice?.TooltipString ?? "";

        private Device? _selectedDevice;
        private bool? _autoStart;
        private ProgressBarWindow? _pbw;
        private readonly ILogger _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly TrayIconManager _trayIcon;
        private Window _childDialog;
        private readonly System.Timers.Timer _updateTimer = new();

        public MainWindow(ILogger<MainWindow> logger, ILoggerFactory loggerFactory)
        {
            InitializeComponent();
            DataContext = this;

            _logger = logger;
            _loggerFactory = loggerFactory;
            _trayIcon = new TrayIconManager(TaskbarIcon);

            _updateTimer.Enabled = false;
            _updateTimer.Interval = UpdatePollPeriodMsec;
            _updateTimer.Elapsed += UpdateTimerElapsed;
            _updateTimer.Start();

            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _trayIcon.UpdateTaskbarIcon(null);
            UpdateDevices();
        }


        public bool AutoStart
        {
            get
            {
                if (_autoStart == null)
                {
                    var registryKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                    _autoStart = registryKey?.GetValue("MagicStickUI") != null;
                }

                return _autoStart ?? false;
            }
            set
            {
                var registryKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

                if (registryKey == null)
                {
                    return;
                }

                if (value)
                {
                    var fileName = Process.GetCurrentProcess().MainModule?.FileName;
                    if (fileName != null) 
                        registryKey.SetValue("MagicStickUI", Path.Combine(AppContext.BaseDirectory, fileName));
                }
                else
                {
                    registryKey.DeleteValue("MagicStickUI", false);
                }

                _autoStart = value;
            }
        }

        private void UpdateTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (SelectedDevice == null || !SelectedDevice.IsSupportedDevice)
                return;

            // Update battery status
            ReadDeviceBattery(SelectedDevice);
            _trayIcon.UpdateTaskbarIcon(SelectedDevice);
            
            OnPropertyChanged(nameof(TooltipString));
        }

        public Device SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                _logger.LogDebug("SelectedDevice.set");

                if (_selectedDevice != value)
                {
                    // Update battery status
                    ReadDeviceBattery(value);
                    _trayIcon.UpdateTaskbarIcon(value);                    

                    // Set the last selected device
                    Properties.Settings.Default.LastSelectedDeviceId = value?.DeviceId;
                    Properties.Settings.Default.Save();

                    // Update _selectedDevice
                    _selectedDevice = value;
                }
            }
        }
        
        private void UpdateDevices()
        {
            _logger.LogDebug("UpdateDevices()");

            foreach (var device in Devices.ToList())
            {
                if (!device.Connected)
                {
                    device.Dispose();
                    Devices.Remove(device);
                }
            }

            var hidDevices = HidDevices
                .Enumerate()
                .Where(t => t.Attributes.VendorId == Constants.VendorIdMagicStick && t.Attributes.ProductId == Constants.ProductIdMagicStick);

            // Group multiple HID endpoints by device serial (all belonging to the same device)
            var groupedHids = new Dictionary<string, List<HidDevice>>();
            foreach (var hd in hidDevices)
            {
                if (hd.ReadSerialNumber(out var value))
                {
                    var deviceId = Util.GetHidString(value);

                    if (!groupedHids.ContainsKey(deviceId))
                        groupedHids[deviceId] = new List<HidDevice>();

                    groupedHids[deviceId].Add(hd);
                }
            }
            
            foreach (var group in groupedHids)
            {
                var deviceId = group.Key;
                var hidEndpoints = group.Value;

                var dev = Devices.FirstOrDefault(t => t.DeviceId == deviceId);
                if (dev == null)
                {
                    dev = new Device(_loggerFactory, deviceId, hidEndpoints.ToArray());                    
                    _logger.LogDebug($"Adding new device: {deviceId}");

                    dev.ChargerDeviceEndpointStateChanged += ChargerDeviceEndpointStateChanged;
                    dev.RpcDeviceEndpointStateChanged += RpcDeviceEndpointStateChanged;
                    dev.RpcEventReceived += RpcEventReceived;

                    Devices.Add(dev);
                }
                else
                {
                    dev.UpdateDeviceDetails();
                    
                    // Trigger DeviceList update
                    Devices.Remove(dev);
                    Devices.Add(dev);
                }

                var asmVersion = typeof(MainWindow).Assembly.GetName().Version;
                if (!dev.IsSupportedDevice)
                {
                    MessageBox.Show($"Device {dev.DeviceName} is incompatible and may not function correctly because this {Constants.AppName} utility only supports version {asmVersion.Major}.*.* devices.",
                        Constants.AppName, MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
            }

            var found = Devices.FirstOrDefault(t => t.DeviceId == Properties.Settings.Default.LastSelectedDeviceId);
            SelectedDevice = null; // Set to null to force a property update
            SelectedDevice = found ?? Devices.FirstOrDefault();            
        }

        private void ChargerDeviceEndpointStateChanged(object? sender, DeviceEndpointStateChangedEventArgs e)
        {
            if (sender is Device dev && dev == SelectedDevice)
            {
                if (e.Connected)
                    ReadDeviceBattery(dev);
                else
                    dev.BatteryPercentage = 0;

                _trayIcon.UpdateTaskbarIcon(dev);
                OnPropertyChanged(nameof(TooltipString));
            }
        }

        private void RpcDeviceEndpointStateChanged(object? sender, DeviceEndpointStateChangedEventArgs e)
        {

        }

        private void RpcEventReceived(object sender, RpcEventArgs e)
        {
            if (sender is Device dev && dev == SelectedDevice)
            {
                if (e.Name == "connected")
                {
                    ReadDeviceBattery(dev);

                    _trayIcon.UpdateTaskbarIcon(dev);
                    OnPropertyChanged(nameof(TooltipString));
                }
                else if (e.Name == "disconnected")
                {
                    dev.BatteryPercentage = 0;

                    _trayIcon.UpdateTaskbarIcon(dev);
                    OnPropertyChanged(nameof(TooltipString));
                }
                else if (e.Name == "send_unicode_char_event")
                {
                    var ucEvt = JsonConvert.DeserializeObject<SendUnicodeCharEvent>(e.Payload);
                    KeyboardInputSender.SendUnicodeToActiveWindow(ucEvt.key_code);
                }                
            }
        }

        private void ReadDeviceBattery(Device dev)
        {
            _logger.LogDebug("ReadDeviceBattery()");

            if (dev == null)
                return;

            if (!dev.IsSupportedDevice)
            {
                dev.BatteryPercentage = 0;
                return;
            }

            try
            {
                if (!dev.ChargerDeviceEndpoint.IsOpen)
                    dev.ChargerDeviceEndpoint.OpenDevice();

                var task = Task.Run(() =>
                {
                    var rep = dev.ChargerDeviceEndpoint.ReadReportSync(0x90);
                    if (rep.ReadStatus != HidDeviceData.ReadStatus.Success)
                        return;
                    dev.BatteryPercentage = rep.Data[1];
                });

                if (!task.Wait(TimeSpan.FromSeconds(5)))
                {
                    _logger.LogDebug("ReadDeviceBattery(): timeout");
                }
            }
            catch (Exception m)
            {
                _logger.LogError(m, $"ReadDeviceBattery(): {m.Message}");
            }
            finally
            {
                dev.ChargerDeviceEndpoint.CloseDevice();
            }
        }

        #region EventHandlers
        private void ExitButton_OnClick(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void DeviceSelect_OnClick(object sender, RoutedEventArgs e)
        {
            var mi = sender as MenuItem;
            if (mi == null)
                return;

            SelectedDevice = (Device)mi.DataContext;
            e.Handled = true;
        }

        private void ScanDevices_OnClick(object sender, RoutedEventArgs e)
        {
            UpdateDevices();
        }

        private void About_OnClick(object sender, RoutedEventArgs e)
        {            
            MessageBox.Show(this, Util.GetVersionString(), Constants.AppName);
        }        

        private void GetInfo_OnClick(object sender, RoutedEventArgs e)
        {
            if (SelectedDevice == null)
                return;

            if (_childDialog != null)
            {
                _childDialog.Close();
                _childDialog = null;
            }

            _childDialog = new DeviceInfoWindow(SelectedDevice) { Owner = this };
            _childDialog.ShowDialog();
        }

        private void DeviceKeymap_OnClick(object sender, RoutedEventArgs e)
        {
            using (new ScopedAction(() => _updateTimer.Enabled = false, () => _updateTimer.Enabled = true))
            {
                if (SelectedDevice == null)
                    return;

                if (_childDialog != null)
                {
                    _childDialog.Close();
                    _childDialog = null;
                }

                _childDialog = new EditorWindow(SelectedDevice.Rpc) { Owner = this };
                if (_childDialog.ShowDialog() == true)
                {
                    
                }
            }
        }

        private void DeviceSettings_OnClick(object sender, RoutedEventArgs e)
        {
            if (SelectedDevice == null)
                return;

            if (_childDialog != null)
            {
                _childDialog.Close();
                _childDialog = null;
            }

            try
            {
                var settings = SelectedDevice.Rpc.GetSettings().GetAwaiter().GetResult();

                var model = new DeviceSettingsViewModel { SwapFnCtrl = settings.swap_fn_ctrl, SwapAltCmd = settings.swap_alt_cmd, BluetoothDisabled = settings.bluetooth_disabled, IoTiming = settings.io_timing };
                _childDialog = new DeviceSettingsWindow(model) { Owner = this };

                if (_childDialog.ShowDialog() == true)
                {
                    var req = new SetSettingsRequest { swap_fn_ctrl = model.SwapFnCtrl, bluetooth_disabled = model.BluetoothDisabled, swap_alt_cmd = model.SwapAltCmd, io_timing = model.IoTiming };
                    SelectedDevice.Rpc.SetSettings(req).GetAwaiter().GetResult();                    
                }
            }
            catch (Exception m)
            {
                MessageBox.Show(m.Message,Constants.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Save_OnClick(object sender, RoutedEventArgs e)
        {
            if (SelectedDevice == null)
                return;

            SelectedDevice.Rpc.SaveConfig().GetAwaiter().GetResult();
        }

        private async void CheckUpdate_OnClick(object sender, RoutedEventArgs e)
        {
            if (SelectedDevice == null)
                return;

            Release msRelease;
            try
            {
                msRelease = await AzureUtil.GetLatestRelease(SelectedDevice.DeviceSerialNumber, Constants.MagicStickFirmwareId);
            }
            catch (Exception m)
            {
                MessageBox.Show($"Update cancelled. Failed to check for updates ({m.Message}).", 
                    Constants.AppName, MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }

            var latestVersion = SemVersion.Parse(msRelease?.SemVer, SemVersionStyles.Any);
            var versionComparison = SelectedDevice.FirmwareSemVer.ComparePrecedenceTo(latestVersion);
            if (versionComparison != 0)
            {
                string msg = null;
                var flashAction = (SelectedDevice.FirmwareId == Constants.MagicStickInitFirmwareId || versionComparison < 0) ? "Upgrade" : "DOWNGRADE";
                if (SelectedDevice.FirmwareId != Constants.MagicStickInitFirmwareId)
                    msg = $"Your current firmware version is: {SelectedDevice.FirmwareSemVer}.\r\n";
                msg += $"{flashAction} device to the latest official firmware version: {latestVersion}?\r\n";
                msg += $"\r\nPlease carefully check the {latestVersion} firmware release notes before proceeding and\r\nalso update the {Constants.AppName} if required by the new firmware.\r\n";

                var box = new CustomMessageBox(
                    msg,
                    Constants.AppName,
                    "Release Notes",                      
                    t => { Process.Start(new ProcessStartInfo(Properties.Settings.Default.ReleaseNotesUrl) { UseShellExecute = true }); });

                var res = box.ShowDialog();
                if (res == true)
                {
                    var updateCancellationTokenSource = new CancellationTokenSource();
                    _pbw = new ProgressBarWindow();
                    _pbw.Title = Constants.AppName;
                    _pbw.Owner = this;                    
                    _pbw.Closed += (o, a) => updateCancellationTokenSource.Cancel();
                    _pbw.SetUserText("Please re-insert the device in BOOTSEL mode (insert while pressing its button) to begin...");

                    _pbw.Show();

                    try
                    {
                        var piRoot = await Util.GetRpDriveRoot(60, updateCancellationTokenSource.Token);
                        if (piRoot == null)
                        {
                            _pbw.Close();
                            MessageBox.Show("Failed to detect device in update mode.", 
                                Constants.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }

                        _pbw.SetUserText($"Flashing: {msRelease.SemVer}...");

                        var deviceId = SelectedDevice.DeviceId;
                        var downloadUri = string.Format(Properties.Settings.Default.DownloadUriTemplate, deviceId, msRelease.Filename);
                        await Util.DownloadFileAsync(downloadUri,
                            Path.Combine(piRoot, msRelease.Filename), p =>
                            {
                                _pbw.SetProgress((int)p);
                            });

                        _pbw.Close();
                        MessageBox.Show("Update completed successfully.", 
                            Constants.AppName, MessageBoxButton.OK, MessageBoxImage.Information);

                        UpdateDevices();
                    }
                    catch (TaskCanceledException)
                    {
                    }
                    catch (Exception m)
                    {
                        _pbw.Close();
                        MessageBox.Show($"Update failed. {m.Message}.", 
                            Constants.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Your device is running the latest available firmware.",
                    Constants.AppName, MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void InitializeDevice_OnClick(object sender, RoutedEventArgs e)
        {
            if (SelectedDevice == null)
                return;

            try
            {
                var updateCancellationTokenSource = new CancellationTokenSource();
                _pbw = new ProgressBarWindow();
                _pbw.Title = Constants.AppName;
                _pbw.Owner = this;
                _pbw.Closed += (o, a) => updateCancellationTokenSource.Cancel();
                _pbw.SetUserText("Please re-insert the device in BOOTSEL mode (insert while pressing its button) to begin...");
                _pbw.Show();

                var piRoot = await Util.GetRpDriveRoot(60, updateCancellationTokenSource.Token);
                if (piRoot == null)
                {
                    _pbw.Close();
                    MessageBox.Show("Failed to detect a device in BOOTSEL mode. Please consult the user manual.", 
                        Constants.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                _pbw.SetUserText("Flashing device initialization firmware...");

                const string fileName = $"{Constants.MagicStickInitFirmwareId}-latest.uf2";
                var downloadUri = string.Format(Properties.Settings.Default.DownloadUriTemplate, "0000000000000000", fileName);
                await Util.DownloadFileAsync(downloadUri,
                    Path.Combine(piRoot, fileName), p =>
                    {
                        _pbw.SetProgress((int)p);
                    });

                _pbw.Close();
                MessageBox.Show("Initialization completed successfully.", 
                    Constants.AppName, MessageBoxButton.OK, MessageBoxImage.Information);

                UpdateDevices();
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception m)
            {
                _pbw.Close();
                MessageBox.Show($"Initialization failed. {m.Message}.", 
                    Constants.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }
        #endregion
    }

    public class PresentationDeviceIsSelectedConverter : IMultiValueConverter
    {
        // OneWay
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            return (values[0] as Device)?.DeviceId == (values[1] as Device)?.DeviceId;
        }

        // TwoWay
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
