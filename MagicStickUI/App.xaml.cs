using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Threading;
using System.Windows;
using Serilog;
using Serilog.Events;

namespace MagicStickUI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static Mutex? _mutex;
        private const string MutexName = "MagicStickUISingleInstanceMutex";
        private IHost? _host;
        private const string LogFileName = "magicstick-log.txt";
        private readonly string _logFilePath;

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

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Debug)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.File(_logFilePath, rollingInterval: RollingInterval.Infinite)
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
                Log.Logger.Error(e.ToString());
                MessageBox.Show($"Sorry, MagicStickUI crashed. There is a crash log saved at: {_logFilePath}.", "MagicStickUI", MessageBoxButton.OK, MessageBoxImage.Error);
            } 
            catch 
            { 
            }
        }
    }
}
