using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Tauri.Plugin.DotNet;
using MagicStickUI.Backend.Models;
using MagicStickUI.Backend.Util;

namespace MagicStickUI.Backend.Services;

/// <summary>
/// Bridge service exposing application-level operations to the frontend. Window, tray-icon
/// application and dialog chrome itself is handled by Tauri's own APIs (see src-tauri/src/lib.rs
/// and the frontend's src/startup.ts and lib/TrayIconManager.ts) - this service only covers what
/// only .NET can do: blending the tray icon's PNG layers, and the app's own persisted settings.
/// </summary>
[BridgeService]
public class AppService
{
    private readonly TrayIconRenderer _trayIcon;

    public AppService(TrayIconRenderer trayIcon)
    {
        _trayIcon = trayIcon;
    }

    /// <summary>
    /// Returns application and runtime version information.
    /// </summary>
    public VersionInfo GetVersionInfo()
    {
        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var appVersion = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? asm.GetName().Version?.ToString()
                      ?? "unknown";

        return new VersionInfo
        {
            AppVersion = appVersion,
            DotNetVersion = RuntimeInformation.FrameworkDescription,
        };
    }

    /// <summary>
    /// Returns true if the app is running on Windows (e.g. to show Windows-specific UI like "Start with Windows").
    /// </summary>
    public bool IsWindows()
    {
        return OperatingSystem.IsWindows();
    }

    /// <summary>
    /// Blends multiple icon PNGs and returns the result as PNG bytes (base64 in JSON).
    /// <paramref name="iconPaths"/> are filenames like "Battery_dark.png", "Indicator_90.png".
    /// The frontend passes the result straight to Tauri's tray icon API.
    /// </summary>
    public byte[] BlendIconData(string[] iconPaths)
    {
        return _trayIcon.BlendIconData(iconPaths);
    }

    private const string AppKeyPath = @"SOFTWARE\MagicStickUI";
    private const string StartMinimizedValueName = "StartMinimized";

    /// <summary>
    /// Returns true if the app is set to start minimized (window hidden at launch).
    /// Only applies on Windows. On non-Windows platforms, always returns false.
    /// </summary>
    public bool GetStartMinimized()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        using var key = Registry.CurrentUser.OpenSubKey(AppKeyPath, false);
        if (key?.GetValue(StartMinimizedValueName) is int i)
            return i != 0;
        return false;
    }

    /// <summary>
    /// Enables or disables start minimized. When enabled, the main window is hidden at startup (Windows only).
    /// No-op on non-Windows platforms.
    /// </summary>
    public void SetStartMinimized(bool enable)
    {
        if (!OperatingSystem.IsWindows())
            return;

        using var key = Registry.CurrentUser.CreateSubKey(AppKeyPath, true);
        key?.SetValue(StartMinimizedValueName, enable ? 1 : 0, RegistryValueKind.DWord);
    }
}
