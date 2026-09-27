using HidLibrary;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Newtonsoft.Json;
using PropertyChanged;
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

namespace MagicStickUI;

[AddINotifyPropertyChangedInterface]
public partial class MainWindow : Window
{
    public ObservableCollection<Device> Devices { get; } = new();

    [PropertyChanged.DependsOn(nameof(SelectedDevice))]
    public bool HasConnectedDevice => SelectedDevice is { Connected: true };

    [PropertyChanged.DependsOn(nameof(HasConnectedDevice))]
    public bool HasRealConnectedDevice => HasConnectedDevice && string.Equals(Constants.MagicStickFirmwareId, SelectedDevice?.FirmwareId, StringComparison.OrdinalIgnoreCase);


    [PropertyChanged.DependsOn(nameof(HasConnectedDevice))]
    public string TooltipString => HasConnectedDevice ? $"{SelectedDevice.DeviceName}, {BatteryLevel}%" : "Disconnected";

    private int? _batteryLevel;
    public int? BatteryLevel 
    { 
        get => _batteryLevel;
        set
        {
            if (_batteryLevel != value)
            {
                _batteryLevel = value;
                
                // Update tray icon on UI thread
                Dispatcher.Invoke(() => _trayIcon.UpdateTaskbarIcon(_batteryLevel, _batteryStatus));

                OnPropertyChanged(nameof(BatteryLevel));
            }
        }
    }

    private int? _batteryStatus;
    public int? BatteryStatus
    {
        get => _batteryStatus;
        set
        {
            if (value != _batteryStatus)
            {
                _batteryStatus = value;

                // Update tray icon on UI thread
                Dispatcher.Invoke(() => _trayIcon.UpdateTaskbarIcon(_batteryLevel, _batteryStatus));

                OnPropertyChanged(nameof(BatteryStatus));
            }
        }
    }


    private Device _selectedDevice;
    private bool? _autoStart;
    
    private readonly ILogger _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly TrayIconManager _trayIcon;
    private Window _childDialog;
    private CancellationTokenSource _batteryReaderCancellation = new();
    private Task _batteryReaderTask;

    public MainWindow(ILogger<MainWindow> logger, ILoggerFactory loggerFactory)
    {
        InitializeComponent();
        DataContext = this;

        _logger = logger;
        _loggerFactory = loggerFactory;
        _trayIcon = new TrayIconManager(TaskbarIcon);

        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _trayIcon.UpdateTaskbarIcon(null, null);
        UpdateDevices();

        // Start the continuous battery monitoring task
        _batteryReaderTask = Task.Factory.StartNew(async () => await UpdateBatteryPercentageTask(), _batteryReaderCancellation.Token);
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

    public Device SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            _logger.LogDebug("SelectedDevice.set");

            if (_selectedDevice != value)
            {
                // Set the last selected device
                Properties.Settings.Default.LastSelectedDeviceId = value?.DeviceId;
                Properties.Settings.Default.Save();

                // Update _selectedDevice
                _selectedDevice = value;

                SafeFireAndForget(RequestChargerReport);
            }
        }
    }

    /// <summary>
    /// Safely executes an async method without awaiting it, catching and logging any exceptions
    /// </summary>
    /// <param name="asyncAction">The async method to execute</param>
    private void SafeFireAndForget(Func<Task> asyncAction)
    {
        _ = asyncAction().ContinueWith(task =>
        {
            if (task.IsFaulted)
            {
                _logger.LogError(task.Exception?.GetBaseException(), "Async operation failed");
            }
        }, TaskScheduler.Default);
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

                var blacklisted = Properties.Settings.Default.BlacklistDevices.Split(",");
                if (blacklisted.Any(t => string.Equals(deviceId, t.Trim(), StringComparison.OrdinalIgnoreCase)))
                    continue;

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
                dev.RequestReportByIdEndpointStateChanged += RequestReportByIdEndpointStateChanged;
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
        }

        var found = Devices.FirstOrDefault(t => t.DeviceId == Properties.Settings.Default.LastSelectedDeviceId);
        SelectedDevice = null; // Set to null to force a property update
        SelectedDevice = found ?? Devices.FirstOrDefault();            
    }

    private void ChargerDeviceEndpointStateChanged(object? sender, DeviceEndpointStateChangedEventArgs e)
    {
        if (sender is Device dev && dev == _selectedDevice)
        {
            if (e.Connected)
            {
                    
            }
            else
            {
                BatteryLevel = null;
                BatteryStatus = null;
            }
            OnPropertyChanged(nameof(TooltipString));
        }
    }

    private void RequestReportByIdEndpointStateChanged(object? sender, DeviceEndpointStateChangedEventArgs e)
    {
        if (sender is Device dev && dev == _selectedDevice)
        {
            if (e.Connected)
            {
                SafeFireAndForget(RequestChargerReport);
            }
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
                OnPropertyChanged(nameof(TooltipString));
            }
            else if (e.Name == "disconnected")
            {
                BatteryLevel = null;
                BatteryStatus = null;
                OnPropertyChanged(nameof(TooltipString));
            }
            else if (e.Name == "send_unicode_char_event")
            {
                var ucEvt = JsonConvert.DeserializeObject<SendUnicodeCharEvent>(e.Payload);
                KeyboardInputSender.SendUnicodeToActiveWindow(ucEvt.key_code);
            }                
        }
    }

    private async Task<bool> RequestChargerReport()
    {
        // Open request device endpoint if needed and send battery report request
        if (_selectedDevice?.Connected != true)
        {
            _logger.LogDebug("Request device endpoint is not connected");
            return false;
        }

        if (!_selectedDevice.RequestReportByIdDeviceEndpoint.IsOpen)
        {
            try
            {
                _selectedDevice.RequestReportByIdDeviceEndpoint.OpenDevice();
                _logger.LogDebug("Opened request device endpoint in overlapped mode");

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open request device endpoint in overlapped mode");
                return false;
            }
        }

        try
        {
            var report = new HidReport(8) { ReportId = Constants.ReportRequestById, Data = [Constants.ReportIdCharger, 0, 0, 0, 0, 0, 0, 0] };
            var writeSuccess = _selectedDevice.RequestReportByIdDeviceEndpoint.WriteReport(report);
            if (!writeSuccess)
            {
                _logger.LogWarning("Failed to send battery report request command - WriteReport returned false");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while sending battery report request command");
            return false;
        }

        _logger.LogDebug("Successfully sent battery report request command");
        return true;
    }

    private async Task UpdateBatteryPercentageTask()
    {
        while (!_batteryReaderCancellation.Token.IsCancellationRequested)
        {
            try
            {
                // Check if we have a connected device
                if (_selectedDevice != null && _selectedDevice?.Connected == true)
                {
                    // Open charger device endpoint if needed
                    if (!_selectedDevice.ChargerDeviceEndpoint.IsOpen)
                    {
                        try
                        {
                            _selectedDevice.ChargerDeviceEndpoint.OpenDevice();
                            _logger.LogDebug("Opened charger endpoint");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to open charger device endpoint");

                            BatteryLevel = null;
                            BatteryStatus = null;

                            await Task.Delay(1000, _batteryReaderCancellation.Token);

                            continue;
                        }
                    }

                    var rep = _selectedDevice.ChargerDeviceEndpoint.ReadReport(1000);
                    if (rep.ReadStatus == HidDeviceData.ReadStatus.WaitTimedOut)
                        continue;

                    if (rep.ReadStatus == HidDeviceData.ReadStatus.Success)
                    {
                        var status = rep.Data[0];
                        var level = rep.Data[1];                        
                        _logger.LogDebug($"Battery level read: {level}%, status: {status}");

                        BatteryLevel = level;
                        BatteryStatus = status;

                        continue;
                    }

                    _logger.LogDebug($"Read failed: {rep.ReadStatus}");                    
                }
                else
                {
                    _logger.LogDebug("No connected device available for battery monitoring");
                }
            }
            catch (OperationCanceledException)
            {
                // Task was cancelled
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in battery monitoring loop");
            }

            BatteryLevel = null;
            BatteryStatus = null;

            await Task.Delay(1000, _batteryReaderCancellation.Token);
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