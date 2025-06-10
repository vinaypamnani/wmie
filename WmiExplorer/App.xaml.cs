using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using MessageBox = System.Windows.MessageBox;
using WmiExplorer.Integration.AvalonEdit.Behaviors;
using WmiExplorer.Integration.PropertyTypeProvider;
using WmiExplorer.Presentation.ViewModels.Coordinators;
using WmiExplorer.Presentation.Views;
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
        var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
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

        // Register the CommunityToolkit messenger as a singleton
        services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);

        // Register core services first
        services.AddSingleton<IMessengerService, MessengerService>();
        services.AddSingleton<ISettingsService, SettingsService>(provider =>
            new SettingsService(provider.GetRequiredService<IMessengerService>()));
        services.AddSingleton<ThemeManager>(provider =>
            new ThemeManager(
                provider.GetRequiredService<IMessengerService>(),
                provider.GetRequiredService<ISettingsService>()));
        services.AddSingleton<ICacheService, CacheService>();
        services.AddSingleton<IWmiService, WmiService>(provider =>
            new WmiService(provider.GetRequiredService<ICacheService>()));
        services.AddSingleton<IApplicationService, ApplicationService>();

        // Register MainWindow for DI
        services.AddSingleton<MainWindow>();

        // Register all ViewModel classes for DI - order matters for dependencies
        services.AddSingleton<WmiInstancesTabViewModel>();
        services.AddSingleton<WmiClassesTabViewModel>();
        services.AddSingleton<WmiNamespacePaneViewModel>();
        services.AddSingleton<OptionsViewModel>();
        services.AddSingleton<PropertyGridViewModel>();
        services.AddSingleton<MainViewModel>();

        services.AddSingleton<Presentation.ViewModels.Watcher.WmiWatcherViewModel>();
        services.AddSingleton<WmiMethodViewModel>();
        services.AddSingleton<WmiPropertyViewModel>();

        // Register additional ViewModels that have multiple instances
        services.AddTransient<WmiQueryViewModel>();
        services.AddTransient<WmiSearchViewModel>();


        // Build the service provider
        ServiceProvider = services.BuildServiceProvider();

        // Set up DI for AvalonEdit behaviors using static method
        var messengerService = ServiceProvider.GetRequiredService<IMessengerService>();
        var settingsService = ServiceProvider.GetRequiredService<ISettingsService>();
        AvalonEditThemingBehavior.SetMessengerService(messengerService);
        AvalonEditWqlHighlightingBehavior.SetMessengerService(messengerService);
        AvalonEditWqlHighlightingBehavior.SetSettingsService(settingsService);

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