using System.Windows;
using DailyUptimeWidget.Services;
using DailyUptimeWidget.ViewModels;
using Application = System.Windows.Application; // Fix ambiguity

namespace DailyUptimeWidget
{
    public partial class App : Application
    {
        private ITrayService? _trayService;
        private static System.Threading.Mutex? _mutex = null;
        private const string MutexName = "Global\\DailyUptimeWidget_SingleInstance_Mutex";

        public static void Log(string message)
        {
            try 
            {
                var appDataPath = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "DailyUptimeWidget");
                System.IO.Directory.CreateDirectory(appDataPath);
                var logPath = System.IO.Path.Combine(appDataPath, "debug_log.txt");
                System.IO.File.AppendAllText(logPath, $"{System.DateTime.Now}: {message}\n", System.Text.Encoding.UTF8);
            }
            catch {}
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            // Global Exception Handling (UI Thread)
            this.DispatcherUnhandledException += (s, ev) => 
            {
                Log($"UNHANDLED DISPATCHER EXCEPTION: {ev.Exception.Message}\n{ev.Exception.StackTrace}");
                ev.Handled = true; 
            };

            // Global Exception Handling (Non-UI Threads)
            System.AppDomain.CurrentDomain.UnhandledException += (s, ev) => 
            {
                if (ev.ExceptionObject is System.Exception ex)
                    Log($"UNHANDLED DOMAIN EXCEPTION: {ex.Message}\n{ex.StackTrace}");
            };

            // Task Exceptions
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, ev) => 
            {
                Log($"UNOBSERVED TASK EXCEPTION: {ev.Exception.Message}\n{ev.Exception.StackTrace}");
                ev.SetObserved();
            };

            // Ensure Working Directory is the App Directory
            System.IO.Directory.SetCurrentDirectory(System.AppDomain.CurrentDomain.BaseDirectory);

            // Single Instance Check
            bool createdNew;
            _mutex = new System.Threading.Mutex(true, MutexName, out createdNew);

            if (!createdNew)
            {
                Log("Another instance is already running. Shutting down.");
                Shutdown();
                return;
            }

            base.OnStartup(e);

            try 
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;

                _trayService = new TrayService();
                
                var mainWindow = new MainWindow();
                
                _trayService.Initialize(
                    onShowCommand: () => 
                    {
                        mainWindow.Show();
                        mainWindow.WindowState = WindowState.Normal;
                        mainWindow.Activate();
                    },
                    onExitCommand: () => 
                    {
                        _trayService.Dispose();
                        Shutdown();
                    });

                var bootService = new BootTimeService();
                var registryService = new StartupRegistryService();
                var viewModel = new MainViewModel(bootService, registryService, _trayService);

                mainWindow.DataContext = viewModel;
                mainWindow.Show();
                Log("Startup Complete.");
            }
            catch (System.Exception ex)
            {
                Log($"CRASH: {ex.Message}\n{ex.StackTrace}");
                System.Windows.MessageBox.Show($"Error starting application: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }
    }
}
