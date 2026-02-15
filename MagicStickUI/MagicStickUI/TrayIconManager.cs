using System;
using System.Drawing;
using System.Runtime.InteropServices;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;

namespace MagicStickUI;

public class TrayIconManager
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);

    private static Bitmap Blank => Util.GetBitmapResource("Blank.png");
    private static Bitmap Battery => IsLightTheme ? Util.GetBitmapResource("Battery.png") : Util.GetBitmapResource("Battery_dark.png");
    private static Bitmap Missing => IsLightTheme ? Util.GetBitmapResource("Missing.png") : Util.GetBitmapResource("Missing_dark.png");
    private static Bitmap Charging => IsLightTheme ? Util.GetBitmapResource("Charging.png") : Util.GetBitmapResource("Charging_dark.png");
    private static Bitmap Charged => IsLightTheme ? Util.GetBitmapResource("Charged.png") : Util.GetBitmapResource("Charged_dark.png");

    private readonly TaskbarIcon _tbi;

    public TrayIconManager(TaskbarIcon tbi)
    {
        _tbi = tbi;
    }

	private static Bitmap MixBitmap(params Image[] images)
	{
		if (images == null || images.Length == 0)
			throw new ArgumentException("At least one image is required.", nameof(images));

		var baseImage = images[0];
		var bitmap = new Bitmap(baseImage.Width, baseImage.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

		using var canvas = Graphics.FromImage(bitmap);

		canvas.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
		canvas.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
		canvas.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

		var baseScaleX = canvas.DpiX / baseImage.HorizontalResolution;
		var baseScaleY = canvas.DpiY / baseImage.VerticalResolution;

		// Draw the base image to fill the canvas
		canvas.DrawImage(baseImage, 0, 0, baseImage.Width * baseScaleX, baseImage.Height * baseScaleY);

		// Draw subsequent images centered on top
		for (int i = 1; i < images.Length; i++)
		{
			var overlay = images[i];
			if (overlay == null) continue;

			var scaleX = canvas.DpiX / overlay.HorizontalResolution;
			var scaleY = canvas.DpiY / overlay.VerticalResolution;

			var x = (bitmap.Width - overlay.Width * scaleX) / 2;
			var y = (bitmap.Height - overlay.Height * scaleY) / 2;

			canvas.DrawImage(overlay, x, y, overlay.Width * scaleX, overlay.Height * scaleY);
		}

		return bitmap;
	}

    private static Bitmap ErrorBitMap()
    {
        return MixBitmap(Battery, Missing);
    }

    private static Icon CreateIcon(int? batteryLevel, int? batteryStatus)
    {
        var batteryLevelBitmap = batteryLevel switch
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
            _ => Blank
        };

        var batteryStatusBitmap = batteryStatus switch
        {
            Constants.BatteryStatusCharging => Charging,
            Constants.BatteryStatusCharged => Charged,
            _ => Blank
        };

        var output = MixBitmap(Battery, batteryLevelBitmap, batteryStatusBitmap);

        return Icon.FromHandle(output.GetHicon());
    }

    public void UpdateTaskbarIcon(int? batteryLevel, int? batteryStatus)
    {
        var oldIcon = _tbi.Icon;
        _tbi.Icon = batteryLevel == null ? Icon.FromHandle(ErrorBitMap().GetHicon()) : CreateIcon(batteryLevel, batteryStatus);

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