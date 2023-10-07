using System.Windows;

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
        }

        public void UpdateProgress(int percentage)
        {
            // When progress is reported, update the progress bar control.
            pbLoad.Value = percentage;

            // When progress reaches 100%, close the progress bar window.
            if (percentage == 100)
            {
                Close();
            }
        }
    }
}
