using System.Runtime.InteropServices;
using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Threading;

namespace MagicStickUI
{
    /// <summary>
    /// Interaction logic for ProgressBarWindow.xaml
    /// </summary>
    public partial class ProgressBarWindow : Window
    {
        public ProgressBarWindow()
        {
            InitializeComponent();

            var iconUri = new Uri("pack://application:,,,/Resources/Chip.png", UriKind.RelativeOrAbsolute);
            Icon = BitmapFrame.Create(iconUri);
        }

        public void SetProgress(int percentage)
        {
            // When progress is reported, update the progress bar control.
            pbLoad.Value = percentage;

            // When progress reaches 100%, close the progress bar window.
            if (percentage == 100)
                Close();
        }

        public void SetUserText(string text)
        {
            tbText.Text = text;
        }
    }
}
