using Semver;
using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;

namespace MagicStickUI;

public static class Util
{
    public static Bitmap GetBitmapResource(string resourceName)
    {
        var asm = typeof(TrayIconManager).Assembly;
        using var stream = asm.GetManifestResourceStream(asm.GetName().Name + ".Resources." + resourceName);
        if (stream == null)
            throw new InvalidOperationException($"Required resource \"{resourceName}\" not found.");

        return new Bitmap(stream);
    }

    public static string GetStringResource(string resourceName)
    {
        var asm = typeof(TrayIconManager).Assembly;
        var fullResourceName = asm.GetName().Name + ".Resources." + resourceName;
        using var stream = asm.GetManifestResourceStream(fullResourceName);
        if (stream == null)
            throw new FileNotFoundException("Resource not found: " + fullResourceName);
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }

    public static (string? deviceNameId, SemVersion) GetSemVerFromDeviceName(string deviceName)
    {
        var idx = deviceName.IndexOf('.');
        string deviceNameId = null;
        string deviceNameVersion = null;
        if (idx > 0)
        {
            deviceNameId = deviceName.Substring(0, idx);
            deviceNameVersion = deviceName.Substring(idx + 1);
        }

        return (deviceNameId, SemVersion.Parse(deviceNameVersion, SemVersionStyles.Any));
    }

    public static string GetHidString(byte[] bytes)
    {
        var str = string.Empty;
        foreach (var b in bytes)
            if (b > 0)
                str += ((char)b).ToString();
        return str;
    }

    public static string GetVersionString()
    {
        var asm = typeof(MainWindow).Assembly;
        var asmVersion = asm.GetName().Version;
        var informationalVersion = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var version = $"{Constants.AppName}." + (informationalVersion ?? $"{asmVersion?.Major}.{asmVersion?.Minor}.{asmVersion?.Build}");

        return version;
    }

    public static string ConvertBytesToString(byte[] data)
    {
        if (data == null)
            return string.Empty;

        var hexString = new StringBuilder();
        for (var i = 0; i < data.Length; i++)
        {
            hexString.Append(data[i].ToString("X2")); // Convert byte to hexadecimal string
            if (i < data.Length - 1)
            {
                hexString.Append(" "); // Add space separator between bytes
            }
        }

        return hexString.ToString();
    }
}