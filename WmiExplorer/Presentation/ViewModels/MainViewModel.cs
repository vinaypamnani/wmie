using System.Windows.Input;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Shared;
using WmiExplorer.Services;
using WmiExplorer.Themes;

namespace WmiExplorer.Presentation.ViewModels;

public class MainViewModel : MessagingViewModelBase
{
    private readonly CancellationTokenSource _cts = new();
    private ApplicationState _currentApplicationState = ApplicationState.Ready();
    private string _elapsedTimeMessage = string.Empty;
    private readonly Coordinators.WmiNamespacePaneViewModel _namespacePaneViewModel;
    private readonly Coordinators.OptionsViewModel _optionsViewModel;
    private readonly Coordinators.PropertyGridViewModel _propertyGridViewModel;
    private int _selectedTabIndex;
    private readonly ISettingsService _settingsService;
    private readonly ThemeManager _themeManager;
    private MainWindowPosition _windowPosition;

    public MainViewModel(
              IMessagingService messagingService,
              ISettingsService settingsService,
              ThemeManager themeManager,
              Coordinators.WmiNamespacePaneViewModel namespacePaneViewModel,
              Coordinators.OptionsViewModel optionsViewModel,
              Coordinators.PropertyGridViewModel propertyGridViewModel)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
        _namespacePaneViewModel = namespacePaneViewModel ?? throw new ArgumentNullException(nameof(namespacePaneViewModel));
        _optionsViewModel = optionsViewModel ?? throw new ArgumentNullException(nameof(optionsViewModel));
        _propertyGridViewModel = propertyGridViewModel ?? throw new ArgumentNullException(nameof(propertyGridViewModel));

        // Initialize messaging
        InitializeMessaging(messagingService);

        // Initialize window position from settings
        _windowPosition = _settingsService.MainWindowPosition;

        ExitCommand = new RelayCommand(_ => Environment.Exit(0));
        ToggleThemeCommand = new RelayCommand(_ => _themeManager.ToggleTheme());

        // Subscribe to messages
        StrongSubscribe<ApplicationStateMessage>(HandleApplicationStateMessage);
        StrongSubscribe<JumpToClassMessage>(HandleJumpToClassMessage);
        StrongSubscribe<ElapsedTimeMessage>(HandleElapsedTimeMessage);

        // Subscribe to theme change messages
        StrongSubscribe<ThemeChangedMessage>(_ =>
        {
            OnPropertyChanged(nameof(ThemeToggleText)); // To update theme toggle text on theme change.
        });
    }

    /// <summary>
    /// The current application state
    /// </summary>
    public ApplicationState CurrentApplicationState
    {
        get => _currentApplicationState;
        set => SetProperty(ref _currentApplicationState, value);
    }

    /// <summary>
    /// Gets the current theme object
    /// </summary>
    public Theme CurrentTheme => _themeManager.CurrentThemeObject!;

    /// <summary>
    /// Elapsed time message for long-running operations
    /// </summary>
    public string ElapsedTimeMessage
    {
        get => _elapsedTimeMessage;
        set => SetProperty(ref _elapsedTimeMessage, value);
    }

    /// <summary>
    /// Command to exit the application
    /// </summary>
    public ICommand ExitCommand { get; }

    /// <summary>
    /// Gets the namespace pane view model
    /// </summary>
    public Coordinators.WmiNamespacePaneViewModel NamespacePaneViewModel => _namespacePaneViewModel;

    /// <summary>
    /// Gets the options view model
    /// </summary>
    public Coordinators.OptionsViewModel OptionsViewModel => _optionsViewModel;

    /// <summary>
    /// Gets the property grid view model
    /// </summary>
    public Coordinators.PropertyGridViewModel PropertyGridViewModel => _propertyGridViewModel;

    /// <summary>
    /// Gets or sets the selected tab index for the main window
    /// </summary>
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    /// <summary>
    /// Flag indicating whether system classes should be shown
    /// </summary>
    public bool ShowSystemClasses
    {
        get => _namespacePaneViewModel.ClassesTabViewModel.ShowSystemClasses;
        set => _namespacePaneViewModel.ClassesTabViewModel.ShowSystemClasses = value;
    }

    /// <summary>
    /// Gets the text for the theme toggle button
    /// </summary>
    public string ThemeToggleText => _themeManager.CurrentThemeName == "Dark" ? "🌙 Dark" : "🌞 Light";

    /// <summary>
    /// Command to toggle between light and dark theme
    /// </summary>
    public ICommand ToggleThemeCommand { get; }

    /// <summary>
    /// Gets the view model for the WMI Event Watcher
    /// </summary>
    public WmiWatcherViewModel WatcherViewModel => _namespacePaneViewModel.WatcherViewModel;

    /// <summary>
    /// Gets the window position settings
    /// </summary>
    public MainWindowPosition WindowPosition
    {
        get => _windowPosition;
        set => SetProperty(ref _windowPosition, value);
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
}