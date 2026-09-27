using Microsoft.Extensions.Logging;
using Tauri.Plugin.DotNet;
using MagicStickUI.Backend.MagicStick;
using MagicStickUI.Backend.Services;
using MagicStickUI.Backend.Util;

namespace MagicStickUI.Backend;

/// <summary>
/// The entry point the plugin's in-process host finds in this assembly: it starts the HID device
/// manager and registers the bridge services. The dispatcher is already wired to the frontend by
/// the time Configure runs, so DeviceManagerService can emit events as soon as devices are found.
/// </summary>
public sealed class Backend : IBridgeBackend
{
    private readonly DeviceManager _deviceManager;

    public ILoggerFactory LoggerFactory { get; } =
        Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder
            .AddSimpleConsole(options => options.SingleLine = true)
            .SetMinimumLevel(LogLevel.Debug));

    public Backend()
    {
        _deviceManager = new DeviceManager(LoggerFactory);
    }

    public void Configure(BridgeDispatcher dispatcher)
    {
        dispatcher.RegisterService(new AppService(new TrayIconRenderer()));
        dispatcher.RegisterService(new DeviceManagerService(dispatcher, _deviceManager));

        _deviceManager.Start();
    }

    // Tauri ends the process without giving .NET a shutdown notification of its own (see the
    // plugin's README "Shutting down"), so this is the only reliable place to stop the HID
    // read loops and release the USB handles.
    public Task ShutdownAsync(CancellationToken cancellationToken)
    {
        _deviceManager.Stop();
        return Task.CompletedTask;
    }
}
