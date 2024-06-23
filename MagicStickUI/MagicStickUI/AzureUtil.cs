using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace MagicStickUI
{
    public static class AzureUtil
    {
        private const string GetLatestVersionInfoUri = "https://magicstick-app.azurewebsites.net/api/latest-release/{0}?packageId={1}";

        public static async Task<Release?> GetLatestRelease(string deviceId, string packageId = "magicstick")
        {
            using var client = new HttpClient();
            var response = await client.GetAsync(string.Format(GetLatestVersionInfoUri, deviceId, packageId));

            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsAsync<Release>();

            throw new Exception($"Failure to download firmware file, HTTP status code: {response.StatusCode}");
        }

    }

    public class Release
    {
        public string? PartitionKey { get; set; }
        public string? RowKey { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public string? ETag { get; set; }

        public string? Filename { get; set; }
        public string? SemVer { get; set; }
    }
}
