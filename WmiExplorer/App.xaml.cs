using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using WmiExplorer.Presentation.Controls.PropertyGrid.WmiProviders;
using WmiExplorer.Services;
using WmiExplorer.Themes;
using MessageBox = System.Windows.MessageBox;

namespace WmiExplorer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        /// <summary>
        /// Configures the service locator with all required services
        /// </summary>
        private void ConfigureServices()
        {
            var serviceLocator = ServiceLocator.Instance;

            // Register core services first
            serviceLocator.Register<IMessagingService, MessagingService>(new MessagingService());

            // Register the settings service before theme manager
            var messagingService = serviceLocator.Get<IMessagingService>();
            serviceLocator.Register<ISettingsService, SettingsService>(new SettingsService(messagingService));

            // Register theme manager with settings service
            var settingsService = serviceLocator.Get<ISettingsService>();
            serviceLocator.Register<ThemeManager, ThemeManager>(new ThemeManager(settingsService));

            // Register WmiService
            serviceLocator.Register<IWmiService, WmiService>(new WmiService());

            // Register ApplicationService
            serviceLocator.Register<IApplicationService, ApplicationService>(new ApplicationService());

            // Configure unhandled exception handling
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var exception = e.ExceptionObject as Exception ?? new Exception("Unknown unhandled exception");
                HandleUnhandledException(exception, "Unhandled Exception");
            };

            DispatcherUnhandledException += (s, e) =>
            {
                HandleUnhandledException(e.Exception, "Unhandled Dispatcher Exception");
                e.Handled = true;
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                HandleUnhandledException(e.Exception, "Unobserved Task Exception");
                e.SetObserved();
            };
        }

        private string GetErrorLogPath()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WmiExplorer");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return Path.Combine(dir, "error.log");
        }

        private void HandleUnhandledException(Exception exception, string title)
        {
            string message = $"An unexpected error occurred: {exception?.Message}\n\n{exception?.StackTrace}";
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            try
            {
                var logPath = GetErrorLogPath();
                File.AppendAllText(logPath, $"[{DateTime.Now}] {title}: {exception}\n");
            }
            catch { /* Ignore logging errors */ }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ConfigureServices();

            // Initialize theme
            var themeManager = ServiceLocator.Instance.Get<ThemeManager>();
            themeManager.InitializeTheme();

            // Register WMI providers for PropertyGrid
            WmiProviderModule.RegisterWmiProviders();
        }
    }
}