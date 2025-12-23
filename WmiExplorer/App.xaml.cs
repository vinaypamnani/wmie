using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using WmiExplorer.Common.Helpers;
using WmiExplorer.Common.Logging;
using WmiExplorer.Integration.AvalonEdit.Behaviors;
using WmiExplorer.Integration.PropertyGrid;
using WmiExplorer.Integration.PropertyTypeProvider;
using WmiExplorer.Presentation.Behaviors;
using WmiExplorer.Presentation.ViewModels.Coordinators;
using WmiExplorer.Presentation.ViewModels.Shared;
using WmiExplorer.Presentation.Views;
using WmiExplorer.PropertyGrid.Abstractions;
using WmiExplorer.PropertyGrid.Providers;
using WmiExplorer.Services;

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
                Log.Debug($"MenuDropAlignment set to {ifLeft}");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error setting MenuDropAlignment");
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        // Enable debug logging to console if -debug is present
        if (CommandLineArgumentsParser.HasFlag(e.Args, "debug"))
        {
            AttachConsole(ATTACH_PARENT_PROCESS);
            Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));
            Trace.AutoFlush = true;
            Trace.WriteLine("\n=== DEBUG logging enabled ===\n");
        }

        base.OnStartup(e);

        // Initialize logging first
        Log.ConfigureLogging();

        // Then configure services
        ConfigureServices();
        if (ServiceProvider == null)
            throw new InvalidOperationException("ServiceProvider is not initialized.");

        // Set the minimum log level from settings
        var settingsService = ServiceProvider.GetRequiredService<ISettingsService>();
        Log.SetMinimumLevel(settingsService.LogLevel);

        // Initialize theme first (loads current accent colors from file)
        var themeManager = ServiceProvider.GetRequiredService<ThemeManager>();
        themeManager.InitializeTheme();

        // Check if app version is higher than stored version and reset themes if needed
        var themesWereReset = CheckForVersionUpdateAndResetThemes(settingsService);

        // Set the current version in auto-update settings
        settingsService.AutoUpdateSettings.CurrentVersion = VersionInfo.AppVersion;

        // Re-apply theme if it was reset (to ensure reset themes are applied)
        if (themesWereReset)
        {
            themeManager.ApplyTheme(themeManager.CurrentThemeName);
        }

        // Register Base WMI providers for PropertyGrid using the new generic method
        ProviderModule.RegisterProvider(new WmiPropertyTypeProvider(), new WmiPropertyValueConverter());

        // Register WMI-specific property editor as singleton for proper disposal
        var wmiPropertyEditor = ServiceProvider.GetRequiredService<WmiPropertyEditor>();
        PropertyEditorRegistry.Instance.RegisterEditor(wmiPropertyEditor);

        // Set menu drop alignment to false
        SetMenuDropAlignment();

        // Create and show MainWindow using DI
        var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();

        // Handle command-line arguments for automatic connection
        var startupConnectionService = ServiceProvider.GetRequiredService<IStartupConnectionService>();
        startupConnectionService.HandleCommandLineConnection(e.Args, mainWindow);
    }

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int dwProcessId);

    private bool CheckForVersionUpdateAndResetThemes(ISettingsService settingsService)
    {
        var currentAppVersion = VersionInfo.AppVersion;
        var storedVersion = settingsService.AutoUpdateSettings.CurrentVersion;

        if (!string.IsNullOrEmpty(storedVersion) && !string.IsNullOrEmpty(currentAppVersion))
        {
            if (Version.TryParse(currentAppVersion, out var current) && Version.TryParse(storedVersion, out var stored))
            {
                if (current > stored)
                {
                    Log.Information($"App updated from version {stored} to {current}. Resetting themes to defaults, preserving accent colors.");
                    ThemeManager.ResetAllThemesToDefaultsPreservingAccents();
                    return true; // Indicate that themes were reset
                }
                else if (current == stored)
                {
                    Log.Debug($"App version unchanged: {currentAppVersion}");
                }
                else
                {
                    Log.Warning($"Stored version {stored} is higher than current app version {current}. This may indicate a downgrade or version mismatch.");
                }
            }
            else
            {
                Log.Warning($"Version parsing failed. Stored: '{storedVersion}', Current: '{currentAppVersion}'");
            }
        }
        else
        {
            Log.Information($"First run or no stored version. Current app version: {currentAppVersion}");
        }
        return false; // Indicate that themes were not reset
    }

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
        services.AddSingleton<ISettingsService, SettingsService>(provider => new SettingsService(provider.GetRequiredService<IMessengerService>()));
        services.AddSingleton<ICacheService, CacheService>();
        services.AddSingleton<IWmiService, WmiService>(provider => new WmiService(provider.GetRequiredService<ICacheService>(), provider.GetRequiredService<ISettingsService>()));
        services.AddSingleton<IApplicationService, ApplicationService>();
        services.AddSingleton<IStartupConnectionService, StartupConnectionService>(provider => new StartupConnectionService(provider));

        // Register UpdateService for GitHub update checks
        // TODO: Change repo name when ready for public release
        services.AddSingleton(provider => new UpdateService("vinaypamnani", "wmie2"));

        // Register managers - order matters for dependencies
        services.AddSingleton<ThemeManager>();
        services.AddSingleton<PropertyGridManager>();
        services.AddSingleton<SelectionManager>();
        services.AddSingleton<SettingsManager>();
        services.AddSingleton<UpdateManager>();

        // Register WMI property editor as singleton for proper disposal
        services.AddSingleton<WmiPropertyEditor>();

        // Register MainWindow for DI
        services.AddSingleton<MainWindow>();

        // Register all ViewModel classes for DI - order matters for dependencies
        services.AddSingleton<InstancesTabViewModel>();
        services.AddSingleton<MethodsTabViewModel>();
        services.AddSingleton<PropertiesTabViewModel>();
        services.AddSingleton<ClassesTabViewModel>();
        services.AddSingleton<NamespacesViewModel>();
        services.AddSingleton<OptionsViewModel>();
        services.AddSingleton<WatcherTabViewModel>();
        services.AddSingleton<LogTabViewModel>();
        services.AddSingleton<QueryTabViewModel>();
        services.AddSingleton<SearchTabViewModel>();
        services.AddSingleton<MainViewModel>();

        // Build the service provider
        ServiceProvider = services.BuildServiceProvider();

        // Set up DI for AvalonEdit behaviors using static method
        var messengerService = ServiceProvider.GetRequiredService<IMessengerService>();
        var settingsService = ServiceProvider.GetRequiredService<ISettingsService>();
        var selectionManager = ServiceProvider.GetRequiredService<SelectionManager>();

        // Set up AvalonEdit behaviors with DI
        AvalonEditThemingBehavior.SetMessengerService(messengerService);
        AvalonEditWqlHighlightingBehavior.SetMessengerService(messengerService);
        AvalonEditWqlHighlightingBehavior.SetSettingsService(settingsService);

        // Set up ItemSelectionBehavior with DI
        ItemSelectionBehavior.SetSelectionManager(selectionManager);

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

    private void HandleUnhandledException(Exception exception, string title)
    {
        string message = $"{title}: {exception?.Message}\n\n{exception?.StackTrace}";
        Debug.WriteLine($"[Unhandled Exception] {message}");
        Log.Error(exception!, message);

        // Ensure the application exits after showing the error
        Application.Current?.Shutdown();
    }
}