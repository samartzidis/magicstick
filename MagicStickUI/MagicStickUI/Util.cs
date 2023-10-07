using Semver;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace MagicStickUI
{
    public static class Util
    {
        public static async Task<string?> GetRpDriveRoot()
        {
            for (int k = 0; k < 10; k++)
            {
                var allDrives = DriveInfo.GetDrives();
                foreach (DriveInfo drive in allDrives)
                {
                    if (drive.IsReady && drive.VolumeLabel.Equals("RPI-RP2", StringComparison.OrdinalIgnoreCase))
                        return drive.RootDirectory.FullName;
                }

                await Task.Delay(1000);
            }

            return null;
        }

        public static SemVersion GetSemVerFromDeviceName(string deviceName)
        {
            var idx = deviceName.IndexOf('.');
            if (idx > 0)
                deviceName = deviceName.Substring(idx + 1);
            return SemVersion.Parse(deviceName, SemVersionStyles.Any);
        }

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
    }
}
