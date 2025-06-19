using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Logging;
using WmiExplorer.Common.Messages;
using WmiExplorer.Common.Models;
using WmiExplorer.Presentation.Themes;
using WmiExplorer.Presentation.ViewModels.Items;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Coordinators;

/// <summary>
/// Main ViewModel for the application
/// </summary>
public partial class MainViewModel : MessagingViewModelBase
{
    private readonly CancellationTokenSource _cts = new();

    [ObservableProperty]
    private ApplicationState _currentApplicationState = ApplicationState.Ready();

    [ObservableProperty]
    private Theme _currentTheme = null!;

    [ObservableProperty]
    private string _elapsedTimeMessage = string.Empty;

    [ObservableProperty]
    private LogTabViewModel _logTabViewModel = null!;

    [ObservableProperty]
    private NamespacesViewModel _namespacesViewModel = null!;

    [ObservableProperty]
    private OptionsViewModel _optionsViewModel = null!;

    [ObservableProperty]
    private PropertyGridViewModel _propertyGridViewModel = null!;

    [ObservableProperty]
    private int _selectedTabIndex;

    private readonly ISelectionService _selectionService;
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;

    [ObservableProperty]
    private string _themeToggleText = string.Empty;

    [ObservableProperty]
    private MainWindowPosition _windowPosition;

    public MainViewModel(
                       IMessengerService messengerService,
                       ISettingsService settingsService,
                       ISelectionService selectionService,
                       IThemeService themeService,
                       NamespacesViewModel namespacesViewModel,
                       OptionsViewModel optionsViewModel,
                       PropertyGridViewModel propertyGridViewModel,
                       LogTabViewModel logTabViewModel) : base(messengerService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _selectionService = selectionService ?? throw new ArgumentNullException(nameof(selectionService));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _namespacesViewModel = namespacesViewModel ?? throw new ArgumentNullException(nameof(namespacesViewModel));
        _optionsViewModel = optionsViewModel ?? throw new ArgumentNullException(nameof(optionsViewModel));
        _propertyGridViewModel = propertyGridViewModel ?? throw new ArgumentNullException(nameof(propertyGridViewModel));
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

        StrongSubscribe<ClassesLoadedMessage>(_ => OnPropertyChanged(nameof(ClassesTabHeader)));
        StrongSubscribe<SelectionChangedMessage>(_ => OnPropertyChanged(nameof(ClassesTabHeader)));

        // Test logging
        Log.Information("Application started successfully");

        // Demonstrate different log levels for testing
        // DemonstrateLogging();
    }

    /// <summary>
    /// Gets the header text for the Classes tab, including class count when available
    /// </summary>
    public string ClassesTabHeader
    {
        get
        {
            var selectedNamespace = NamespacesViewModel?.SelectedNamespace;
            if (selectedNamespace?.ClassLoadState == ClassLoadState.Success && selectedNamespace.Classes != null)
            {
                return $"Classes [{selectedNamespace.Classes.Count}]";
            }
            return "Classes";
        }
    }

    /// <summary>
    /// Override to clean up additional resources
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Cancel any pending operations
            if (!_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }

            // Dispose the cancellation token source
            _cts.Dispose();
        }

        base.Dispose(disposing);
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
            _selectionService.ClearSelections();
        else
            _selectionService.SetSelectedObject(_selectionService.PreviousObject);
    }

    /// <summary>
    /// Command to toggle between light and dark theme
    /// </summary>
    [RelayCommand]
    private void ToggleTheme()
    {
        _themeService.ToggleTheme(); // Theme change message will trigger UpdateThemeProperties via subscription
        Log.Debug("Changed Current theme to: {ThemeName}", _themeService.CurrentTheme?.ThemeName ?? "Unknown");
    }

    /// <summary>
    /// Updates the theme-related properties based on current theme
    /// </summary>
    private void UpdateThemeProperties()
    {
        CurrentTheme = _themeService.CurrentTheme!;
        ThemeToggleText = _themeService.CurrentThemeName == "Dark" ? "🌙 Dark" : "🌞 Light";
    }
}