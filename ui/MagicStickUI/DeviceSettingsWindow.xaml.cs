using HidLibrary;
using System.Windows;
using Microsoft.Extensions.Logging;
using System.Windows.Input;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;

namespace MagicStickUI
{
    public class DeviceSettingsViewModel
    {
        public const uint IoTimingMin = 0;
        public const uint IoTimingDefault = 50;
        public const uint IoTimingMax = 200;


        public bool SwapFnCtrl { get; set; }
        public bool SwapAltCmd { get; set; }
        public bool BluetoothDisabled { get; set; }
        public uint IoTiming { get; set; }
    }

    public partial class DeviceSettingsWindow : Window
    {

        public DeviceSettingsWindow(DeviceSettingsViewModel data)
        {
            InitializeComponent();
            Title = Constants.AppName;

            DataContext = data;
        }

        private void Button_Click_Ok(object sender, RoutedEventArgs e)
        {            
            DialogResult = true;
        }

        private void Button_Click_Cancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void TextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {

        }

        private void NumericOnlyTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!char.IsDigit(e.Text, e.Text.Length - 1))
            {
                e.Handled = true; // Ignore non-numeric characters
            }
        }

        private bool ValidateNumericTextBox(TextBox textBox)
        {
            if (!int.TryParse(textBox.Text, out int value))
                return false;

            return value >= DeviceSettingsViewModel.IoTimingMin && value <= DeviceSettingsViewModel.IoTimingMax;
        }

        private void NumericTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            OkButton.IsEnabled = ValidateNumericTextBox(sender as TextBox);
        }
    }
}
