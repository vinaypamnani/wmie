using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Logging;
using WmiExplorer.Common.Messages;
using WmiExplorer.Common.Models;
using WmiExplorer.Presentation.Themes;
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
    private object? _selectedDebugObject;

    [ObservableProperty]
    private int _selectedTabIndex;

    private readonly SettingsManager _settingsManager;
    private readonly ThemeManager _themeManager;

    [ObservableProperty]
    private UpdateManager _updateManager;

    [ObservableProperty]
    private string _versionText = WmiExplorer.VersionInfo.AppVersion;

    public MainViewModel(
        IMessengerService messengerService,
        ThemeManager themeManager,
        SelectionManager selectionManager,
        SettingsManager settingsManager,
        UpdateManager updateManager,
        NamespacesViewModel namespacesViewModel,
        OptionsViewModel optionsViewModel,
        LogTabViewModel logTabViewModel) : base(messengerService, selectionManager)
    {
        _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
        _namespacesViewModel = namespacesViewModel ?? throw new ArgumentNullException(nameof(namespacesViewModel));
        _optionsViewModel = optionsViewModel ?? throw new ArgumentNullException(nameof(optionsViewModel));
        _logTabViewModel = logTabViewModel ?? throw new ArgumentNullException(nameof(logTabViewModel));
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
        StrongSubscribe<ClassesFilteredMessage>(HandleClassesFilteredMessage);
        StrongSubscribe<InstancesFilteredMessage>(HandleInstancesFilteredMessage);

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
            var count = SelectionManager.SelectedNamespace?.QueryTabViewModel?.Results?.Count ?? 0;
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
            var count = SelectionManager.SelectedNamespace?.SearchTabViewModel?.Results?.Count ?? 0;
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
            var count = NamespacesViewModel?.WatcherTabViewModel?.Events?.Count ?? 0;
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
    /// Handles classes filtered messages to update the status bar with filtered counts
    /// </summary>
    private void HandleClassesFilteredMessage(ClassesFilteredMessage message)
    {
        // Only update status bar if the filtered namespace is the currently selected one
        if (message?.NamespaceViewModel != null && message.NamespaceViewModel == SelectionManager.SelectedNamespace)
        {
            UpdateStatusBarForNamespace(message.NamespaceViewModel);
        }
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
    /// Handles instances filtered messages to update the status bar with filtered counts
    /// </summary>
    private void HandleInstancesFilteredMessage(InstancesFilteredMessage message)
    {
        // Only update status bar if the filtered class is the currently selected one
        if (message?.ClassViewModel != null && message.ClassViewModel == SelectionManager.GetSelectedClass())
        {
            UpdateStatusBarForClass(message.ClassViewModel);
        }
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
        if (value != 0) // Assuming 0 is the index for Classes tab
            SelectionManager.PropertyGrid.ClearPropertyGrid();
        else
        {
            // Set selection in order: Instance, Class, Namespace
            if (SelectionManager.GetSelectedInstance() != null)
            {
                SelectionManager.SetSelectedObject(SelectionManager.GetSelectedInstance(), updatePropertyGrid: true);
            }
            else if (SelectionManager.GetSelectedClass() != null)
            {
                SelectionManager.SetSelectedObject(SelectionManager.GetSelectedClass(), updatePropertyGrid: true);
            }
            else if (SelectionManager.SelectedNamespace != null)
            {
                SelectionManager.SetSelectedObject(SelectionManager.SelectedNamespace, updatePropertyGrid: true);
            }
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

    /// <summary>
    /// Updates status bar for class selection
    /// </summary>
    private void UpdateStatusBarForClass(WmiClassViewModel wmiClass)
    {
        var ns = wmiClass.ParentNamespaceViewModel;

        switch (wmiClass.LoadState)
        {
            case InstanceLoadState.Unknown:
                PublishSuccessState($"Selected class '{wmiClass.ClassName}' in {ns.NamespacePath}. Double-click to load instances.");
                break;
            case InstanceLoadState.Loading:
                PublishBusyState($"Loading instances for class '{wmiClass.ClassName}' in {ns.NamespacePath}...");
                break;
            case InstanceLoadState.Warning:
                var partialCount = wmiClass.InstancesView.Cast<object>().Count();
                PublishWarningState($"Showing partial results ({partialCount} instances) for class '{wmiClass.ClassName}' in {ns.NamespacePath}.");
                break;
            case InstanceLoadState.Success:
                var instanceCount = wmiClass.InstancesView.Cast<object>().Count();
                var totalInstances = wmiClass.Instances.Count;
                if (instanceCount < totalInstances)
                    PublishSuccessState($"Selected class '{wmiClass.ClassName}' - showing {instanceCount} of {totalInstances} instances in {ns.NamespacePath}.");
                else
                    PublishSuccessState($"Selected class '{wmiClass.ClassName}' - {instanceCount} instances in {ns.NamespacePath}.");
                break;
            case InstanceLoadState.Failed:
                PublishErrorState($"Failed to load instances for class '{wmiClass.ClassName}' in {ns.NamespacePath}. Double-click to try again.", wmiClass.LoadException);
                break;
        }
    }

    /// <summary>
    /// Updates status bar for instance selection
    /// </summary>
    private void UpdateStatusBarForInstance(WmiInstanceViewModel instance)
    {
        var ns = instance.ParentNamespace;
        var wmiClass = instance.ParentClass;

        if (ns == null || wmiClass == null)
        {
            PublishSuccessState($"Selected instance: {instance.InstanceName}");
            return;
        }

        switch (instance.LoadState)
        {
            case WmiInstanceViewModel.InstanceState.Success:
                PublishSuccessState($"Selected instance '{instance.InstanceName}' from class '{wmiClass.ClassName}' in {ns.NamespacePath}");
                break;
            case WmiInstanceViewModel.InstanceState.Failed:
                PublishErrorState($"Failed to load instance '{instance.InstanceName}' from class '{wmiClass.ClassName}'");
                break;
            case WmiInstanceViewModel.InstanceState.Unknown:
            default:
                PublishSuccessState($"Selected instance '{instance.InstanceName}' from class '{wmiClass.ClassName}' in {ns.NamespacePath}");
                break;
        }
    }

    /// <summary>
    /// Updates status bar for namespace selection
    /// </summary>
    private void UpdateStatusBarForNamespace(WmiNamespaceViewModel ns)
    {
        // Handle namespace loading failures first
        if (ns.NamespaceLoadState == NamespaceLoadState.Failed)
        {
            PublishErrorState($"Failed to load child namespaces for {ns.NamespacePath}: {ns.LoadException?.Message}", ns.LoadException);
            return;
        }

        // If namespace is not successfully loaded, show loading or other state
        if (ns.NamespaceLoadState == NamespaceLoadState.Loading)
        {
            PublishBusyState($"Loading child namespaces for {ns.NamespacePath}...");
            return;
        }

        if (ns.NamespaceLoadState != NamespaceLoadState.Success)
            return;

        // Show status based on namespace class load state
        switch (ns.ClassLoadState)
        {
            case ClassLoadState.Unknown:
                PublishSuccessState($"Selected namespace {ns.NamespacePath}. Double-click to load classes.");
                break;
            case ClassLoadState.Loading:
                PublishBusyState($"Loading classes for {ns.NamespacePath}...");
                break;
            case ClassLoadState.Warning:
                var partialClassCount = ns.ClassesView.Cast<object>().Count();
                PublishWarningState($"Showing partial results ({partialClassCount} classes) for {ns.NamespacePath}.");
                break;
            case ClassLoadState.Failed:
                PublishErrorState($"Failed to load classes for {ns.NamespacePath}. Double-click to try again.", ns.LoadException);
                break;
            case ClassLoadState.Success:
                var count = ns.ClassesView.Cast<object>().Count();
                var total = ns.Classes.Count;
                if (count < total)
                    PublishSuccessState($"Selected namespace {ns.NamespacePath} - showing {count} of {total} classes.");
                else
                    PublishSuccessState($"Selected namespace {ns.NamespacePath} - {count} classes.");
                break;
        }
    }

    /// <summary>
    /// Updates the status bar based on the most recently selected object and its state.
    /// Provides consistent status messaging patterns across different selection types.
    /// </summary>
    private void UpdateStatusBarForSelection(SelectionManager selectionManager)
    {
        var selectedObject = selectionManager.SelectedObject;

        switch (selectedObject)
        {
            case WmiInstanceViewModel instance:
                UpdateStatusBarForInstance(instance);
                break;
            case WmiClassViewModel wmiClass:
                UpdateStatusBarForClass(wmiClass);
                break;
            case WmiNamespaceViewModel ns:
                UpdateStatusBarForNamespace(ns);
                break;
            default:
                // No selection or unknown selection type - show ready state
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