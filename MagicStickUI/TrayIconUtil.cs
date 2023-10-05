using System;
using System.Drawing;
using Microsoft.Win32;

namespace MagicStickUI
{
    public static class TrayIconUtil
    {
        private static Bitmap Battery => IsLightTheme ? GetBitmapResource("Battery.png") : GetBitmapResource("Battery_dark.png");
        private static Bitmap Missing => IsLightTheme ? GetBitmapResource("Missing.png") : GetBitmapResource("Missing_dark.png");

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

        public static Icon ErrorIcon()
        {
            return Icon.FromHandle(ErrorBitMap().GetHicon());
        }

        public static Icon GenerateIcon(PresentationDevice device)
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
                    > 95 => GetBitmapResource("Indicator_100.png"),
                    > 90 => GetBitmapResource("Indicator_90.png"),
                    > 80 => GetBitmapResource("Indicator_80.png"),
                    > 70 => GetBitmapResource("Indicator_70.png"),
                    > 60 => GetBitmapResource("Indicator_60.png"),
                    > 50 => GetBitmapResource("Indicator_50.png"),
                    > 40 => GetBitmapResource("Indicator_40.png"),
                    > 30 => GetBitmapResource("Indicator_30.png"),
                    > 20 => GetBitmapResource("Indicator_20.png"),
                    > 10 => GetBitmapResource("Indicator_10.png"),
                    > 0 => GetBitmapResource("Indicator_1.png"),
                    _ => Missing
                };

                output = MixBitmap(Battery, indicator);
            }

            return Icon.FromHandle(output.GetHicon());
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

        private static Bitmap GetBitmapResource(string resourceName)
        {
            var asm = typeof(TrayIconUtil).Assembly;
            using var stream = asm.GetManifestResourceStream(asm.GetName().Name + ".Resources." + resourceName);
            if (stream == null)
                throw new InvalidOperationException($"Required resource \"{resourceName}\" not found.");
            return new Bitmap(stream);
        }
    }
}
