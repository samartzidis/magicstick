using System;
using System.Drawing;
using Microsoft.Win32;

namespace MagicStickUI
{
    public static class TrayIconUtil
    {
        private static Bitmap Battery => IsLightTheme ? GetBitmapResource("Battery.png") : GetBitmapResource("Battery_dark.png");
        private static Bitmap Missing => IsLightTheme ? GetBitmapResource("Missing.png") : GetBitmapResource("Missing_dark.png");

        private static Bitmap MixBitmap(Bitmap battery, Bitmap indicator)
        {
            var bitmap = new Bitmap(battery.Width, battery.Height, battery.PixelFormat);
            bitmap.SetResolution(battery.HorizontalResolution, battery.VerticalResolution);

            using var canvas = Graphics.FromImage(bitmap);
            canvas.DrawImage(battery, new Point(0, 0));
            canvas.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

            var x = (battery.Width - indicator.Width) / 2;
            var y = (battery.Height - indicator.Height) / 2;

            canvas.DrawImage(indicator, x, y);

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
