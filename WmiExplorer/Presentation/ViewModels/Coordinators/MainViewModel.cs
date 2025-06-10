using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Shared;
using WmiExplorer.Services;
using WmiExplorer.Themes;

namespace WmiExplorer.Presentation.ViewModels.Coordinators;

/// <summary>
/// Main ViewModel for the application
/// </summary>
public partial class MainViewModel : MessagingViewModel
{
    private readonly CancellationTokenSource _cts = new();

    [ObservableProperty]
    private ApplicationState _currentApplicationState = ApplicationState.Ready();

    [ObservableProperty]
    private Theme _currentTheme = null!;

    [ObservableProperty]
    private string _elapsedTimeMessage = string.Empty;

    [ObservableProperty]
    private Coordinators.NamespacesViewModel _namespacesViewModel = null!;

    [ObservableProperty]
    private Coordinators.OptionsViewModel _optionsViewModel = null!;

    [ObservableProperty]
    private Coordinators.PropertyGridViewModel _propertyGridViewModel = null!;

    [ObservableProperty]
    private int _selectedTabIndex;

    private readonly ISettingsService _settingsService;
    private readonly ThemeManager _themeManager;

    [ObservableProperty]
    private string _themeToggleText = string.Empty;

    [ObservableProperty]
    private MainWindowPosition _windowPosition;

    public MainViewModel(
                 IMessengerService messengerService,
                 ISettingsService settingsService,
                 ThemeManager themeManager,
                 Coordinators.NamespacesViewModel namespacesViewModel,
                 Coordinators.OptionsViewModel optionsViewModel,
                 Coordinators.PropertyGridViewModel propertyGridViewModel) : base(messengerService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
        _namespacesViewModel = namespacesViewModel ?? throw new ArgumentNullException(nameof(namespacesViewModel));
        _optionsViewModel = optionsViewModel ?? throw new ArgumentNullException(nameof(optionsViewModel));
        _propertyGridViewModel = propertyGridViewModel ?? throw new ArgumentNullException(nameof(propertyGridViewModel));

        // Initialize window position from settings
        _windowPosition = _settingsService.MainWindowPosition;

        // Initialize the theme properties
        UpdateThemeProperties();

        // Subscribe to messages with strong references to prevent garbage collection
        StrongSubscribe<ApplicationStateMessage>(HandleApplicationStateMessage);
        StrongSubscribe<JumpToClassMessage>(HandleJumpToClassMessage);
        StrongSubscribe<ElapsedTimeMessage>(HandleElapsedTimeMessage);
        StrongSubscribe<ThemeChangedMessage>(_ => UpdateThemeProperties());
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
        System.Diagnostics.Debug.WriteLine($"Application state changed: {message.State.State}, Message: {message.State.Message}");
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
    /// Command to toggle between light and dark theme
    /// </summary>
    [RelayCommand]
    private void ToggleTheme()
    {
        _themeManager.ToggleTheme();
        // Theme change message will trigger UpdateThemeProperties via subscription
    }

    /// <summary>
    /// Updates the theme-related properties based on current theme
    /// </summary>
    private void UpdateThemeProperties()
    {
        CurrentTheme = _themeManager.CurrentThemeObject!;
        ThemeToggleText = _themeManager.CurrentThemeName == "Dark" ? "🌙 Dark" : "🌞 Light";
    }
}