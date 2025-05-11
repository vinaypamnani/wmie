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
                var exception = e.ExceptionObject as Exception;
                MessageBox.Show(
                    $"An unexpected error occurred: {exception?.Message}\n\n{exception?.StackTrace}",
                    "Unhandled Exception",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            };

            DispatcherUnhandledException += (s, e) =>
            {
                MessageBox.Show(
                    $"An unexpected error occurred: {e.Exception.Message}\n\n{e.Exception.StackTrace}",
                    "Unhandled Exception",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                e.Handled = true;
            };
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