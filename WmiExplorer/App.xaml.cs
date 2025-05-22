using System.IO;
using System.Windows;
using MessageBox = System.Windows.MessageBox;
using WmiExplorer.Presentation.PropertyTypeProvider;
using WmiExplorer.PropertyGrid.Providers;
using WmiExplorer.Services;
using WmiExplorer.Themes;

namespace WmiExplorer;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ConfigureServices();

        // Initialize theme
        var themeManager = ServiceLocator.Instance.Get<ThemeManager>();
        themeManager.InitializeTheme();

        // Register Base WMI providers for PropertyGrid using the new generic method
        var wmiService = ServiceLocator.Instance.Get<IWmiService>();
        ProviderModule.RegisterProvider(new WmiPropertyTypeProvider(wmiService), new WmiPropertyValueConverter());
    }

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

        var settingsService = serviceLocator.Get<ISettingsService>();

        // Register theme manager with messaging and settings service
        serviceLocator.Register<ThemeManager, ThemeManager>(new ThemeManager(messagingService, settingsService));

        // Register cache service
        serviceLocator.Register<ICacheService, CacheService>(new CacheService());
        var cacheService = serviceLocator.Get<ICacheService>();

        // Register WmiService with injected cache service
        serviceLocator.Register<IWmiService, WmiService>(new WmiService(cacheService));

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
}