using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using MessageBox = System.Windows.MessageBox;
using WmiExplorer.Integration.PropertyTypeProvider;
using WmiExplorer.PropertyGrid.Providers;
using WmiExplorer.Services;
using WmiExplorer.Themes;

namespace WmiExplorer;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private const int ATTACH_PARENT_PROCESS = -1;

    public static ServiceProvider? ServiceProvider { get; private set; }

    public static void SetMenuDropAlignment()
    {
        try
        {
            var ifLeft = SystemParameters.MenuDropAlignment;

            if (ifLeft)
            {
                // change to false
                var t = typeof(SystemParameters);
                var field = t.GetField("_menuDropAlignment", BindingFlags.NonPublic | BindingFlags.Static);
                field?.SetValue(null, false);

                ifLeft = SystemParameters.MenuDropAlignment;
                Debug.WriteLine($"[SetMenuDropAlignment] MenuDropAlignment set to {ifLeft}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SetMenuDropAlignment] Error setting MenuDropAlignment: {ex.Message}");
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        // Enable debug logging to console if -v is present
        if (e.Args.Contains("-debug"))
        {
            AttachConsole(ATTACH_PARENT_PROCESS);
            Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));
            Trace.AutoFlush = true;
            Trace.WriteLine("=== DEBUG logging enabled ===");
        }

        base.OnStartup(e);
        ConfigureServices();

        if (ServiceProvider == null)
            throw new InvalidOperationException("ServiceProvider is not initialized.");

        // Initialize theme
        var themeManager = ServiceProvider.GetRequiredService<ThemeManager>();
        themeManager.InitializeTheme();

        // Register Base WMI providers for PropertyGrid using the new generic method
        var wmiService = ServiceProvider.GetRequiredService<IWmiService>();
        ProviderModule.RegisterProvider(new WmiPropertyTypeProvider(wmiService), new WmiPropertyValueConverter());

        // Set menu drop alignment to false
        SetMenuDropAlignment();

        // Create and show MainWindow using DI
        var mainWindow = ServiceProvider.GetRequiredService<Presentation.Views.MainWindow>();
        mainWindow.Show();
    }

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int dwProcessId);

    /// <summary>
    /// Configures the service locator with all required services
    /// </summary>
    private void ConfigureServices()
    {
        var services = new ServiceCollection();

        // Register core services first
        services.AddSingleton<IMessagingService, MessagingService>();
        services.AddSingleton<ISettingsService, SettingsService>(provider =>
            new SettingsService(provider.GetRequiredService<IMessagingService>()));
        services.AddSingleton<ThemeManager>(provider =>
            new ThemeManager(
                provider.GetRequiredService<IMessagingService>(),
                provider.GetRequiredService<ISettingsService>()));
        services.AddSingleton<ICacheService, CacheService>();
        services.AddSingleton<IWmiService, WmiService>(provider =>
            new WmiService(provider.GetRequiredService<ICacheService>()));
        services.AddSingleton<IApplicationService, ApplicationService>();

        // Register MainWindow for DI
        services.AddSingleton<Presentation.Views.MainWindow>();        // Register all ViewModel classes for DI - order matters for dependencies
        services.AddSingleton<WmiExplorer.Presentation.ViewModels.Coordinators.WmiInstancesTabViewModel>();
        services.AddSingleton<WmiExplorer.Presentation.ViewModels.Coordinators.WmiClassesTabViewModel>();
        services.AddSingleton<WmiExplorer.Presentation.ViewModels.Coordinators.WmiNamespacePaneViewModel>();
        services.AddSingleton<WmiExplorer.Presentation.ViewModels.Coordinators.OptionsViewModel>();
        services.AddSingleton<WmiExplorer.Presentation.ViewModels.MainViewModel>();

        services.AddSingleton<WmiExplorer.Presentation.ViewModels.WmiWatcherViewModel>();
        services.AddSingleton<WmiExplorer.Presentation.ViewModels.WmiMethodViewModel>();
        services.AddSingleton<WmiExplorer.Presentation.ViewModels.WmiPropertyViewModel>();

        // Register additional ViewModels that have multiple instances
        services.AddTransient<WmiExplorer.Presentation.ViewModels.WmiQueryViewModel>();
        services.AddTransient<WmiExplorer.Presentation.ViewModels.WmiSearchViewModel>();


        // Build the service provider
        ServiceProvider = services.BuildServiceProvider();

        // Set up DI for AvalonEdit behaviors using static method
        var messagingService = ServiceProvider.GetRequiredService<IMessagingService>();
        var settingsService = ServiceProvider.GetRequiredService<ISettingsService>();
        Integration.AvalonEdit.Behaviors.AvalonEditThemingBehavior.SetMessagingService(messagingService);
        Integration.AvalonEdit.Behaviors.AvalonEditWqlHighlightingBehavior.SetMessagingService(messagingService);
        Integration.AvalonEdit.Behaviors.AvalonEditWqlHighlightingBehavior.SetSettingsService(settingsService);

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

        // Ensure the application exits after showing the error
        Application.Current?.Shutdown();
    }
}