namespace MagicStickUI.Backend.Models;

/// <summary>
/// DTO for version information returned by AppService.GetVersionInfo().
/// </summary>
public class VersionInfo
{
    public string AppVersion { get; set; }
    public string DotNetVersion { get; set; }
}
