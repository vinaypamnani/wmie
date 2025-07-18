using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Logging;
using WmiExplorer.Common.Messages;
using WmiExplorer.Common.Models;
using WmiExplorer.Presentation.Themes;
using WmiExplorer.Presentation.ViewModels.Helpers;
using WmiExplorer.Presentation.ViewModels.Items;
using WmiExplorer.Presentation.ViewModels.Shared;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Coordinators;

/// <summary>
/// Main ViewModel for the application
/// </summary>
public partial class MainViewModel : SelectionAwareViewModelBase
{
    [ObservableProperty]
    private ApplicationState _currentApplicationState = ApplicationState.Ready();

    [ObservableProperty]
    private Theme _currentTheme = null!;

    [ObservableProperty]
    private string _currentThemeName = string.Empty;

    [ObservableProperty]
    private string _elapsedTimeMessage = string.Empty;

    [ObservableProperty]
    private bool _isDebugMode;

    [ObservableProperty]
    private LogTabViewModel _logTabViewModel = null!;

    [ObservableProperty]
    private NamespacesViewModel _namespacesViewModel = null!;

    [ObservableProperty]
    private OptionsViewModel _optionsViewModel = null!;

    [ObservableProperty]
    private QueryTabViewModel _queryTabViewModel = null!;

    [ObservableProperty]
    private SearchTabViewModel _searchTabViewModel = null!;

    [ObservableProperty]
    private object? _selectedDebugObject;

    [ObservableProperty]
    private int _selectedTabIndex;

    private readonly SettingsManager _settingsManager;
    private readonly ThemeManager _themeManager;

    [ObservableProperty]
    private UpdateManager _updateManager;

    [ObservableProperty]
    private string _versionText = WmiExplorer.VersionInfo.AppVersion;

    [ObservableProperty]
    private WatcherTabViewModel _watcherTabViewModel = null!;

    public MainViewModel(
        IMessengerService messengerService,
        ThemeManager themeManager,
        SelectionManager selectionManager,
        SettingsManager settingsManager,
        UpdateManager updateManager,
        NamespacesViewModel namespacesViewModel,
        OptionsViewModel optionsViewModel,
        LogTabViewModel logTabViewModel,
        QueryTabViewModel queryTabViewModel,
        SearchTabViewModel searchTabViewModel,
        WatcherTabViewModel watcherTabViewModel) : base(messengerService, selectionManager)
    {
        _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
        _namespacesViewModel = namespacesViewModel ?? throw new ArgumentNullException(nameof(namespacesViewModel));
        _optionsViewModel = optionsViewModel ?? throw new ArgumentNullException(nameof(optionsViewModel));
        _logTabViewModel = logTabViewModel ?? throw new ArgumentNullException(nameof(logTabViewModel));
        _queryTabViewModel = queryTabViewModel ?? throw new ArgumentNullException(nameof(queryTabViewModel));
        _searchTabViewModel = searchTabViewModel ?? throw new ArgumentNullException(nameof(searchTabViewModel));
        _watcherTabViewModel = watcherTabViewModel ?? throw new ArgumentNullException(nameof(watcherTabViewModel));
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        _updateManager = updateManager ?? throw new ArgumentNullException(nameof(updateManager));

        // Dynamically resolve all relevant view models from ServiceProvider
        DebugObjects = ResolveDebugObjects();

        // Initialize the theme properties
        UpdateThemeProperties();

        // Subscribe to messages with strong references to prevent garbage collection
        StrongSubscribe<ApplicationStateMessage>(HandleApplicationStateMessage);
        StrongSubscribe<JumpToClassMessage>(HandleJumpToClassMessage);
        StrongSubscribe<ElapsedTimeMessage>(HandleElapsedTimeMessage);
        StrongSubscribe<ThemeChangedMessage>(_ => UpdateThemeProperties());

        // Subscribe to events that affect tab headers
        StrongSubscribe<TabCountChangedMessage>(_ => UpdateTabHeaders());

        // Subscribe to SwitchMainTabMessage to handle tab switching requests
        StrongSubscribe<SwitchMainTabMessage>(HandleSwitchMainTabMessage);

        // Test logging
        Log.Information("Application started successfully. IsPortable: {IsPortable}", UpdateManager.IsPortable);

        // Check for updates on startup if enabled and interval has elapsed
        PerformAutoUpdateCheckOnStartup();

        // Demonstrate different log levels for testing
        // DemonstrateLogging();

        // // Test
        // MessageBoxDialog.Show(
        //     "Welcome to WMI Explorer!",
        //     "WMI Explorer",
        //     MessageBoxDialogButton.OK,
        //     MessageBoxDialogIcon.Information,
        //     Application.Current.MainWindow);
    }

    /// <summary>
    /// Gets the header text for the Classes tab with count
    /// </summary>
    public string ClassesTabHeader
    {
        get
        {
            var count = SelectionManager.SelectedNamespace?.Classes?.Count ?? 0;
            return count > 0 ? $"Classes [{count}]" : "Classes";
        }
    }

    public List<object> DebugObjects { get; }

    /// <summary>
    /// Gets the header text for the Log tab with entries count
    /// </summary>
    public string LogTabHeader
    {
        get
        {
            var count = LogTabViewModel?.LogEntries?.Count ?? 0;
            return count > 0 ? $"Log [{count}]" : "Log";
        }
    }

    /// <summary>
    /// Gets the header text for the Query tab with results count
    /// </summary>
    public string QueryTabHeader
    {
        get
        {
            var count = QueryTabViewModel?.Results?.Count ?? 0;
            return count > 0 ? $"Query [{count}]" : "Query";
        }
    }

    /// <summary>
    /// Gets the header text for the Search tab with count
    /// </summary>
    public string SearchTabHeader
    {
        get
        {
            var count = SearchTabViewModel?.Results?.Count ?? 0;
            return count > 0 ? $"Search [{count}]" : "Search";
        }
    }

    public SettingsManager SettingsManager => _settingsManager;

    /// <summary>
    /// Gets the header text for the Watcher tab with events count
    /// </summary>
    public string WatcherTabHeader
    {
        get
        {
            var count = WatcherTabViewModel?.Events?.Count ?? 0;
            return count > 0 ? $"Watcher [{count}]" : "Watcher";
        }
    }

    /// <summary>
    /// Called when the selected class changes. Override from SelectionAwareViewModelBase.
    /// </summary>
    protected override void OnSelectedClassChanged(WmiClassViewModel? selectedClass)
    {
        UpdateStatusBarForSelection(SelectionManager);
    }

    /// <summary>
    /// Called when the selected instance changes. Override from SelectionAwareViewModelBase.
    /// </summary>
    protected override void OnSelectedInstanceChanged(WmiInstanceViewModel? selectedInstance)
    {
        UpdateStatusBarForSelection(SelectionManager);
    }

    /// <summary>
    /// Called when the selected namespace changes. Override from SelectionAwareViewModelBase.
    /// </summary>
    protected override void OnSelectedNamespaceChanged(WmiNamespaceViewModel? selectedNamespace)
    {
        UpdateTabHeaders();
        UpdateStatusBarForSelection(SelectionManager);
    }

    /// <summary>
    /// Demonstrates logging at different levels for testing purposes
    /// </summary>
    private void DemonstrateLogging()
    {
        Log.Debug("Demonstrating debug logging - usually filtered out in production");
        Log.Warning("This is a sample warning message - for demonstration purposes");

        // Simulate an exception for testing
        try
        {
            throw new InvalidOperationException("This is a test exception to show error logging");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Caught test exception in DemonstrateLogging method");
        }

        Log.Information("Logging demonstration completed");
    }

    /// <summary>
    /// Command to exit the application
    /// </summary>
    [RelayCommand]
    private void Exit()
    {
        // Use window close to ensure Closing event is fired and settings are saved
        System.Windows.Application.Current.MainWindow?.Close();
    }

    /// <summary>
    /// Handles application state messages
    /// </summary>
    private void HandleApplicationStateMessage(ApplicationStateMessage message)
    {
        // Ensure application state updates happen on the UI thread
        RunOnUIThread(() =>
        {
            CurrentApplicationState = message.State;
        });

        // Log state change for debugging
        System.Diagnostics.Debug.WriteLine($"[MainViewModel] Application state changed: {message.State.State}, Message: {message.State.Message}");
    }

    /// <summary>
    /// Handles elapsed time messages for long-running operations
    /// </summary>
    private void HandleElapsedTimeMessage(ElapsedTimeMessage message)
    {
        // Ensure elapsed time updates happen on the UI thread
        RunOnUIThread(() =>
        {
            ElapsedTimeMessage = message.Message;
        });
    }

    /// <summary>
    /// Handles JumpToClassMessage to navigate to the correct namespace and class, handling lazy loading and tab switching.
    /// </summary>
    private void HandleJumpToClassMessage(JumpToClassMessage message)
    {
        if (message == null)
            return;

        // Switch to Classes tab (assume tab index 0 is Classes)
        SelectedTabIndex = 0;
    }

    /// <summary>
    /// Handles requests to switch the main tab (from other viewmodels)
    /// </summary>
    private void HandleSwitchMainTabMessage(SwitchMainTabMessage message)
    {
        SelectedTabIndex = message.TabIndex;
    }

    partial void OnSelectedDebugObjectChanged(object? value)
    {
        // When changing debug selection, update the property grid
        if (value != null)
        {
            SelectionManager.PropertyGrid.SetPropertyGridObject(value, value.ToString() ?? "Debug Object");
        }
    }

    /// <summary>
    /// Clear selections when the selected tab index changes
    /// </summary>
    partial void OnSelectedTabIndexChanged(int value)
    {
        switch (value)
        {
            case 1:
                // Search Tab
                SelectionManager.PropertyGrid.SetPropertyGridObject(SearchTabViewModel?.SelectedResult);
                break;
            case 2:
                // Query Tab
                SelectionManager.PropertyGrid.SetPropertyGridObject(QueryTabViewModel?.SelectedResult);
                break;
            case 3:
                // Watcher Tab
                SelectionManager.PropertyGrid.SetPropertyGridObject(WatcherTabViewModel?.SelectedEvent);
                break;
            case 4:
                // Log Tab
                SelectionManager.PropertyGrid.SetPropertyGridObject(LogTabViewModel?.SelectedLogEntry);
                break;
            default:
                // Classes Tab - Select Instance/Class/Namespace in that order
                if (SelectionManager.GetSelectedInstance() != null)
                {
                    SelectionManager.PropertyGrid.SetPropertyGridObject(SelectionManager.GetSelectedInstance());
                }
                else if (SelectionManager.GetSelectedClass() != null)
                {
                    SelectionManager.PropertyGrid.SetPropertyGridObject(SelectionManager.GetSelectedClass());
                }
                else if (SelectionManager.SelectedNamespace != null)
                {
                    SelectionManager.PropertyGrid.SetPropertyGridObject(SelectionManager.SelectedNamespace);
                }
                break;
        }
    }

    /// <summary>
    /// Checks for updates on startup if enabled and interval has elapsed.
    /// </summary>
    private void PerformAutoUpdateCheckOnStartup()
    {
        if (_settingsManager.AutoUpdateSettings?.CheckOnStartup == true)
        {
            var now = DateTime.UtcNow;
            var lastCheck = _settingsManager.AutoUpdateSettings?.LastCheckTime;
            var intervalDays = _settingsManager.AutoUpdateSettings?.IntervalDays ?? 0;
            if (!lastCheck.HasValue || (now - lastCheck.Value).TotalDays >= intervalDays)
            {
                UpdateManager.CheckForUpdatesAsync().ConfigureAwait(false);
                _settingsManager.AutoUpdateSettings!.LastCheckTime = now;
            }
            else
            {
                Log.Debug("Skipping update check - last check was {LastCheck} (interval: {IntervalDays} days)", lastCheck.Value, intervalDays);
            }
        }
    }

    /// <summary>
    /// Command to toggle debug mode
    /// </summary>
    [RelayCommand]
    private void RefreshDebugObject()
    {
        SelectionManager.PropertyGrid.RefreshPropertyGrid();
    }

    /// <summary>
    /// Command to reset both Light and Dark themes (preserving accent colors) and refresh the current theme.
    /// </summary>
    [RelayCommand]
    private void ResetTheme()
    {
        try
        {
            _themeManager.ResetThemesPreservingAccentsAndRefresh();
            Log.Information("Theme colors have been reset to defaults (accents preserved).");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to reset themes to default. Remove %appdata%\\WmiExplorer\\themes.json file manually.");
        }
    }

    private List<object> ResolveDebugObjects()
    {
        var provider = App.ServiceProvider;
        if (provider == null)
            return new List<object>();

        // List of types to include in DebugViewModels
        var types = new[]
        {
            // ViewModels
            typeof(NamespacesViewModel),
            typeof(OptionsViewModel),
            typeof(LogTabViewModel),
            typeof(InstancesTabViewModel),
            typeof(ClassesTabViewModel),
            typeof(WatcherTabViewModel),
            typeof(MethodsTabViewModel),
            typeof(PropertiesTabViewModel),
            typeof(QueryTabViewModel),
            typeof(SearchTabViewModel),

            // Managers
            typeof(SelectionManager),
            typeof(SettingsManager),
            typeof(PropertyGridManager),
            typeof(UpdateManager),
            typeof(ThemeManager),

            // Add more as needed
        };
        var result = new List<object>();
        foreach (var type in types)
        {
            try
            {
                var instance = provider.GetService(type);
                if (instance != null)
                    result.Add(instance);
            }
            catch { /* ignore missing */ }
        }

        // Add MainViewModel itself
        result.Add(this);

        return result.OrderBy(x => x.GetType().FullName).ToList();
    }

    /// <summary>
    /// Command to toggle debug mode
    /// </summary>
    [RelayCommand]
    private void ToggleDebugMode()
    {
        IsDebugMode = !IsDebugMode;
    }

    /// <summary>
    /// Command to toggle between light and dark theme
    /// </summary>
    [RelayCommand]
    private void ToggleTheme()
    {
        _themeManager.ToggleTheme(); // Theme change message will trigger UpdateThemeProperties via subscription
        Log.Debug("Changed Current theme to: {ThemeName}", _themeManager.CurrentTheme?.ThemeName ?? "Unknown");
    }

    private void UpdateStatusBarForItemStatus(ItemStatus status, string fallback = "Ready")
    {
        // Check if we already have the same application state to avoid unnecessary updates
        var equivalentAppState = ItemStatus.MapLoadStateToAppState(status.LoadState);
        if (status.StatusMessage == CurrentApplicationState.Message && equivalentAppState == CurrentApplicationState.State)
            return;

        switch (status.LoadState)
        {
            case LoadState.Error:
                PublishErrorState(status.StatusMessage, status.Exception);
                break;
            case LoadState.Expanding:
            case LoadState.Loading:
                PublishBusyState(status.StatusMessage);
                break;
            case LoadState.Success:
                PublishSuccessState(status.StatusMessage);
                break;
            case LoadState.PartialSuccess:
                PublishPartialSuccessState(status.StatusMessage);
                break;
            case LoadState.Warning:
                PublishWarningState(status.StatusMessage);
                break;
            default:
                PublishReadyState(fallback);
                break;
        }
    }

    private void UpdateStatusBarForSelection(SelectionManager selectionManager)
    {
        switch (selectionManager.SelectedObject)
        {
            case WmiNamespaceViewModel ns:
                UpdateStatusBarForItemStatus(ns.ItemStatus);
                break;
            case WmiClassViewModel wmiClass:
                UpdateStatusBarForItemStatus(wmiClass.ItemStatus);
                break;
            case WmiInstanceViewModel instance:
                UpdateStatusBarForItemStatus(instance.ItemStatus);
                break;
            default:
                PublishReadyState("Ready");
                break;
        }
    }

    /// <summary>
    /// Updates tab header property change notifications
    /// </summary>
    private void UpdateTabHeaders()
    {
        OnPropertyChanged(nameof(ClassesTabHeader));
        OnPropertyChanged(nameof(SearchTabHeader));
        OnPropertyChanged(nameof(QueryTabHeader));
        OnPropertyChanged(nameof(WatcherTabHeader));
        OnPropertyChanged(nameof(LogTabHeader));
    }

    /// <summary>
    /// Updates the theme-related properties based on current theme
    /// </summary>
    private void UpdateThemeProperties()
    {
        CurrentTheme = _themeManager.CurrentTheme!;
        CurrentThemeName = _themeManager.CurrentThemeName == "Dark" ? "Dark" : "Light";
        // CurrentThemeName = _themeManager.CurrentThemeName == "Dark" ? "🌙 Dark" : "🌞 Light";
    }
}