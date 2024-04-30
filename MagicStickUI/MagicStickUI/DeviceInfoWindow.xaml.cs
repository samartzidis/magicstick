using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MagicStickUI
{
    /// <summary>
    /// Interaction logic for DeviceInfoWindow.xaml
    /// </summary>
    public partial class DeviceInfoWindow : Window
    {
        public Dictionary<string, string> Data { get; set; } = new();

        public DeviceInfoWindow(PresentationDevice device)
        {
            InitializeComponent();
                       
            Data["Model"] = device.DeviceName;
            Data["Serial"] = device.DeviceSerialNumber;
            Data["Connected"] = device.Connected.ToString();            
            Data["BatteryPercentage"] = $"{device.BatteryPercentage}%";
            Data["ChargerDevicePathEndpoint"] = device.ChargerDevicePathEndpoint;
            Data["ControlDevicePathEndpoint"] = device.ControlDevicePathEndpoint;

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
