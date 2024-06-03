using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MagicStickUI
{
    /// <summary>
    /// Interaction logic for DeviceInfoWindow.xaml
    /// </summary>
    public partial class DeviceInfoWindow : Window
    {
        public Dictionary<string, string> Data { get; set; } = new();

        public DeviceInfoWindow(Device device)
        {
            InitializeComponent();

            Title = Constants.AppName;

            Data["Utility Version"] = Util.GetVersionString();
            Data["Device Model"] = device.DeviceName;
            Data["Device Serial"] = device.DeviceSerialNumber;
            Data["Device Connected"] = device.Connected.ToString();            
            Data["Keyboard Battery Level"] = $"{device.BatteryPercentage}%";
            
            DataContext = this;
        }

        private void DataGrid_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Handle the double-click event to prevent crashing
            e.Handled = true;
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            // Copy the selected cell's content to the clipboard
            if (dataGrid.SelectedCells.Count > 0)
            {
                var selectedCell = dataGrid.SelectedCells[0];
                if (selectedCell.IsValid)
                {
                    Clipboard.SetText(selectedCell.Item.ToString());
                }
            }
        }

        private void DataGrid_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Select the clicked cell
            var cell = sender as DataGridCell;
            if (cell != null)
            {
                cell.Focus();
                var dataGrid = FindVisualParent<DataGrid>(cell);
                if (dataGrid != null)
                {
                    if (!cell.IsSelected)
                    {
                        cell.IsSelected = true;
                    }
                }
            }
        }

        private T FindVisualParent<T>(DependencyObject obj) where T : DependencyObject
        {
            while (obj != null)
            {
                if (obj is T parent)
                {
                    return parent;
                }
                obj = VisualTreeHelper.GetParent(obj);
            }
            return null;
        }
    }
}
