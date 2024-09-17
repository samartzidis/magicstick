using Semver;
using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MagicStickUI
{
    public static class Util
    {
        /*
        public static async Task<string?> GetRpDriveRoot(int maxWaitSec = 10, CancellationToken ct = default)
        {
            for (int k = 0; k < maxWaitSec && !ct.IsCancellationRequested; k++)
            {
                var allDrives = DriveInfo.GetDrives();
                foreach (DriveInfo drive in allDrives)
                {
                    if (drive.IsReady && drive.VolumeLabel.Equals("RPI-RP2", StringComparison.OrdinalIgnoreCase))
                        return drive.RootDirectory.FullName;
                }

                await Task.Delay(1000, ct);
            }

            return null;
        }
        */

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

        /*
        public static async Task DownloadFileAsync(string fileUrl, string savePath, Action<double>? reportProgress = null)
        {
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(fileUrl);
            if (response.IsSuccessStatusCode)
            {
                var totalBytes = response.Content.Headers.ContentLength ?? -1;
                //Console.WriteLine($"Total file size: {totalBytes} bytes");

                const int bufferSize = 81920; // 80 KB buffer
                var buffer = new byte[bufferSize];
                long totalBytesRead = 0;

                await using var fileStream = File.Create(savePath);
                await using var contentStream = await response.Content.ReadAsStreamAsync();
                int bytesRead;
                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, bufferSize)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalBytesRead += bytesRead;

                    if (totalBytes > 0)
                    {
                        var progress = (double)totalBytesRead / totalBytes * 100;
                        //Console.WriteLine($"Progress: {progress:F2}%");
                        if (reportProgress != null)
                            reportProgress(progress);
                    }
                }
            }
            else
            {
                throw new Exception($"Failed to download the file. HTTP status code: {response.StatusCode}");
            }
        }
        */

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
}
