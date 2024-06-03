using System;
using System.Drawing;
using System.Runtime.InteropServices;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;

namespace MagicStickUI
{
    public class TrayIconManager
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        private static Bitmap Battery => IsLightTheme ? Util.GetBitmapResource("Battery.png") : Util.GetBitmapResource("Battery_dark.png");
        private static Bitmap Missing => IsLightTheme ? Util.GetBitmapResource("Missing.png") : Util.GetBitmapResource("Missing_dark.png");

        private readonly TaskbarIcon _tbi;

        public TrayIconManager(TaskbarIcon tbi)
        {
            _tbi = tbi;
        }

        private static Bitmap MixBitmap(Image battery, Image indicator)
        {
            var bitmap = new Bitmap(battery.Width, battery.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            using var canvas = Graphics.FromImage(bitmap);

            canvas.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            canvas.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            canvas.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

            var scaleX = canvas.DpiX / battery.HorizontalResolution;
            var scaleY = canvas.DpiY / battery.VerticalResolution;

            canvas.DrawImage(battery, 0, 0, battery.Width * scaleX, battery.Height * scaleY);

            var x = (bitmap.Width - indicator.Width * scaleX) / 2;
            var y = (bitmap.Height - indicator.Height * scaleY) / 2;

            canvas.DrawImage(indicator, x, y, indicator.Width * scaleX, indicator.Height * scaleY);

            return bitmap;
        }

        private static Bitmap ErrorBitMap()
        {
            return MixBitmap(Battery, Missing);
        }

        private static Icon CreateIcon(Device device)
        {
            Bitmap output;
            if (device is not { Connected: true })
            {
                output = ErrorBitMap();
            }
            else
            {
                var indicator = device.BatteryPercentage switch
                {
                    > 95 => Util.GetBitmapResource("Indicator_100.png"),
                    > 90 => Util.GetBitmapResource("Indicator_90.png"),
                    > 80 => Util.GetBitmapResource("Indicator_80.png"),
                    > 70 => Util.GetBitmapResource("Indicator_70.png"),
                    > 60 => Util.GetBitmapResource("Indicator_60.png"),
                    > 50 => Util.GetBitmapResource("Indicator_50.png"),
                    > 40 => Util.GetBitmapResource("Indicator_40.png"),
                    > 30 => Util.GetBitmapResource("Indicator_30.png"),
                    > 20 => Util.GetBitmapResource("Indicator_20.png"),
                    > 10 => Util.GetBitmapResource("Indicator_10.png"),
                    > 0 => Util.GetBitmapResource("Indicator_1.png"),
                    _ => Missing
                };

                output = MixBitmap(Battery, indicator);
            }

            return Icon.FromHandle(output.GetHicon());
        }

        public void UpdateTaskbarIcon(Device device)
        {
            var oldIcon = _tbi.Icon;
            _tbi.Icon = device == null ? Icon.FromHandle(ErrorBitMap().GetHicon()) : CreateIcon(device);

            if (oldIcon != null)
            {
                DestroyIcon(oldIcon.Handle);
                oldIcon.Dispose();
            }
        }

        private static bool IsLightTheme
        {
            get
            {
                var regPath = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false);
                var regFlag = (int)regPath.GetValue("SystemUsesLightTheme", 0);

                return regFlag != 0;
            }
        }
    }
}
