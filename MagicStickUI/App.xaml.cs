using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Threading;
using System.Windows;
using Microsoft.Extensions.Logging;
using Serilog;

namespace MagicStickUI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private static Mutex? _mutex;
        private const string MutexName = "MagicStickUISingleInstanceMutex";
        private IHost? _host;
        private const string LogFileName = "magicstick-log.txt";
        private readonly string _logFilePath;
        private Microsoft.Extensions.Logging.ILogger _logger;

        public App()
        {
            _mutex = new Mutex(true, MutexName, out var createdNew);
            if (!createdNew)
            {
                MessageBox.Show("Another instance of MagicStickUI is already running.", "MagicStickUI", MessageBoxButton.OK, MessageBoxImage.Error );
                Shutdown();
            }

            _logFilePath = Path.Combine(Path.GetTempPath(), LogFileName);
            AppDomain.CurrentDomain.UnhandledException += CrashHandler;
        }

        public void App_Startup(object sender, StartupEventArgs e)
        {
            if (File.Exists(_logFilePath))
                File.Delete(_logFilePath);

            Serilog.Log.Logger = new Serilog.LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Debug)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.File(_logFilePath, rollingInterval: Serilog.RollingInterval.Infinite)
                .CreateLogger();
            
            _host = Host.CreateDefaultBuilder(e.Args)
                .ConfigureAppConfiguration((context, configuration) =>
                {
                    configuration.SetBasePath(context.HostingEnvironment.ContentRootPath);
                    configuration.AddJsonFile("appsettings.json", optional: true);
                })
                .ConfigureServices(builder => {
                    builder.AddSingleton<MainWindow>();
                })              
                .UseSerilog()
                .Build();

            _logger = _host.Services.GetRequiredService<ILogger<App>>();

            _host.Start();

            var mw = _host.Services.GetRequiredService<MainWindow>();
            mw.ShowActivated = false;
            mw.Show();
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            using (_host)
                _host?.StopAsync().Wait();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _mutex?.ReleaseMutex(); // Release the mutex on application exit
            base.OnExit(e);
        }

        private void CrashHandler(object sender, UnhandledExceptionEventArgs args)
        {
            try 
            {
                var e = (Exception)args.ExceptionObject;
                _logger.LogError(e, e.ToString());

                MessageBox.Show($"Sorry, MagicStickUI just crashed. There is a crash log saved at: {_logFilePath}.", "MagicStickUI", MessageBoxButton.OK, MessageBoxImage.Error);
            } 
            catch 
            { 
            }
        }
    }
}
