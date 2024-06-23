using System;
using System.Windows;

namespace MagicStickUI
{
    public partial class CustomMessageBox : Window
    {
        public MessageBoxResult Result { get; private set; }
        private readonly Action<CustomMessageBox> _userAction;

        public CustomMessageBox(string message, string title, string userButtonText, Action<CustomMessageBox> userAction = null)
        {
            InitializeComponent();
            Title = title;
            txtMessage.Text = message;
            btnCustom.Content = userButtonText;            
            _userAction = userAction;
        }

        public new bool? ShowDialog()
        {
            base.ShowDialog();
            return DialogResult;
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.OK;
            DialogResult = true;

            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Cancel;
            DialogResult = false;

            Close();
        }

        private void btnCustom_Click(object sender, RoutedEventArgs e)
        {
            _userAction?.Invoke(this);
        }
    }
}
