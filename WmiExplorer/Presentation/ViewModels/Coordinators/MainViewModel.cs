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
    private LogTabViewModel _logTabViewModel = null!;

    [ObservableProperty]
    private NamespacesViewModel _namespacesViewModel = null!;

    [ObservableProperty]
    private OptionsViewModel _optionsViewModel = null!;

    [ObservableProperty]
    private int _selectedTabIndex;

    private readonly ISettingsService _settingsService;
    private readonly ThemeManager _themeManager;

    [ObservableProperty]
    private MainWindowPosition _windowPosition;

    public MainViewModel(
        IMessengerService messengerService,
        ISettingsService settingsService,
        ThemeManager themeManager,
        SelectionManager selectionManager,
        NamespacesViewModel namespacesViewModel,
        OptionsViewModel optionsViewModel,
        LogTabViewModel logTabViewModel) : base(messengerService, selectionManager)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
        _namespacesViewModel = namespacesViewModel ?? throw new ArgumentNullException(nameof(namespacesViewModel));
        _optionsViewModel = optionsViewModel ?? throw new ArgumentNullException(nameof(optionsViewModel));
        _logTabViewModel = logTabViewModel ?? throw new ArgumentNullException(nameof(logTabViewModel));

        // Initialize window position from settings
        _windowPosition = _settingsService.MainWindowPosition;

        // Initialize the theme properties
        UpdateThemeProperties();

        // Subscribe to messages with strong references to prevent garbage collection
        StrongSubscribe<ApplicationStateMessage>(HandleApplicationStateMessage);
        StrongSubscribe<JumpToClassMessage>(HandleJumpToClassMessage);
        StrongSubscribe<ElapsedTimeMessage>(HandleElapsedTimeMessage);
        StrongSubscribe<ThemeChangedMessage>(_ => UpdateThemeProperties());

        // Subscribe to events that affect tab headers
        StrongSubscribe<TabCountChangedMessage>(_ => UpdateTabHeaders());

        // Test logging
        Log.Information("Application started successfully");

        // Demonstrate different log levels for testing
        // DemonstrateLogging();
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
    /// Called when the selected namespace changes. Override from SelectionAwareViewModelBase.
    /// </summary>
    protected override void OnSelectedNamespaceChanged(WmiNamespaceViewModel? selectedNamespace)
    {
        UpdateTabHeaders();
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
    private void Exit() => Environment.Exit(0);

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
    /// Clear selections when the selected tab index changes
    /// </summary>
    partial void OnSelectedTabIndexChanged(int value)
    {
        if (value != 0) // Assuming 0 is the index for Classes tab
            SelectionManager.PropertyGrid.ClearPropertyGrid();
        else
            SelectionManager.SetSelectedObject(SelectionManager.PreviousObject, updatePropertyGrid: true);
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