using System.Windows.Input;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Shared;
using WmiExplorer.Presentation.ViewModels.Items;
using WmiExplorer.Services;
using WmiExplorer.Themes;

namespace WmiExplorer.Presentation.ViewModels;

public class MainViewModel : MessagingViewModelBase
{
    private readonly IApplicationService _applicationService;
    private readonly ICacheService _cacheService;
    private readonly CancellationTokenSource _cts = new();
    private ApplicationState _currentApplicationState = ApplicationState.Ready();
    private string _elapsedTimeMessage = string.Empty;
    private readonly Coordinators.WmiNamespacePaneViewModel _namespacePaneViewModel;
    private WmiOperationMode _operationMode = WmiOperationMode.Asynchronous;
    private object? _selectedObject;
    private int _selectedTabIndex;
    private readonly ISettingsService _settingsService;
    private string _temporaryComputerName = Environment.MachineName;
    private readonly ThemeManager _themeManager;
    private WmiWatcherViewModel? _watcherViewModel;
    private MainWindowPosition _windowPosition;
    private readonly IWmiService _wmiService;

    public MainViewModel(
              IMessagingService messagingService,
              ISettingsService settingsService,
              ThemeManager themeManager,
              IWmiService wmiService,
              IApplicationService applicationService,
              ICacheService cacheService,
              WmiWatcherViewModel watcherViewModel,
              Coordinators.WmiNamespacePaneViewModel namespacePaneViewModel)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
        _wmiService = wmiService ?? throw new ArgumentNullException(nameof(wmiService));
        _applicationService = applicationService ?? throw new ArgumentNullException(nameof(applicationService));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _namespacePaneViewModel = namespacePaneViewModel ?? throw new ArgumentNullException(nameof(namespacePaneViewModel));

        // Initialize messaging
        InitializeMessaging(messagingService);

        // Initialize commands
        ConnectCommand = new AsyncRelayCommand(async () =>
            await _namespacePaneViewModel.ConnectAsync(_temporaryComputerName.Trim()));

        // Initialize commands
        ReloadClassesCommand = _namespacePaneViewModel.ReloadClassesCommand;
        ExitCommand = new RelayCommand(_ => Environment.Exit(0));
        ToggleThemeCommand = new RelayCommand(_ => _themeManager.ToggleTheme());

        // Subscribe to messages
        StrongSubscribe<ApplicationStateMessage>(HandleApplicationStateMessage);
        StrongSubscribe<SelectedNamespaceChangedMessage>(HandleSelectedNamespaceChangedMessage);
        StrongSubscribe<SelectedClassChangedMessage>(HandleSelectedClassChangedMessage);
        StrongSubscribe<SelectedInstanceChangedMessage>(HandleSelectedInstanceChangedMessage);
        StrongSubscribe<ClassTypeFilterChangedMessage>(HandleClassTypeFilterChangedMessage);
        StrongSubscribe<SelectedEventChangedMessage>(HandleSelectedEventChangedMessage);
        StrongSubscribe<SelectedSearchResultChangedMessage>(HandleSelectedSearchResultChangedMessage);
        StrongSubscribe<JumpToClassMessage>(HandleJumpToClassMessage);
        StrongSubscribe<ElapsedTimeMessage>(HandleElapsedTimeMessage);
        StrongSubscribe<WmiQueryInstanceChangedMessage>(HandleWmiQueryInstanceChangedMessage);

        // Subscribe to theme change messages
        StrongSubscribe<ThemeChangedMessage>(_ =>
        {
            OnPropertyChanged(nameof(CurrentTheme)); // To update color-picker color on theme change.
            OnPropertyChanged(nameof(ThemeToggleText)); // To update theme toggle text on theme change.
        });

        // Initialize window position from settings
        _windowPosition = _settingsService.MainWindowPosition;

        // Initialize the singleton Event Watcher ViewModel
        _watcherViewModel = watcherViewModel ?? throw new ArgumentNullException(nameof(watcherViewModel));

        // Log initial class filter
        System.Diagnostics.Debug.WriteLine($"Initialized ClassTypeFilter from settings: {_settingsService.ClassTypeFilter}");

        _settingsService.ShowSystemClassesChanged += (s, v) =>
        {
            OnPropertyChanged(nameof(ShowSystemClasses));
        };
    }

    /// <summary>
    /// Filter for WMI class types - now wraps the settings service
    /// </summary>
    public WmiClassTypeFlags ClassTypeFilter
    {
        get => _settingsService.ClassTypeFilter;
        set
        {
            // Get current value for comparison
            var currentValue = _settingsService.ClassTypeFilter;
            var newValue = value;

            // Check if the incoming value is actually a negative flag value from our converter
            // Negative values indicate a flag needs to be cleared
            if ((int)value < 0)
            {
                // This is a signal from our converter that we need to clear a flag
                // Convert the negative value back to a positive flag by taking its complement again
                var flagToClear = (WmiClassTypeFlags)(~(int)value);

                // Clear the specific flag while preserving all other flags
                newValue = currentValue & ~flagToClear;
            }
            else if ((int)value > 0 && (int)value <= (int)WmiClassTypeFlags.All)
            {
                // This is a positive flag value coming from the converter when a checkbox is checked
                // Set this flag while preserving all other flags
                newValue = currentValue | value;
            }

            // Only update if the value actually changed
            if (currentValue != newValue)
            {
                // Update the setting - this will handle save and notifications
                _settingsService.ClassTypeFilter = newValue;

                // Notify UI of property change
                OnPropertyChanged(nameof(ClassTypeFilter));

                System.Diagnostics.Debug.WriteLine($"ClassTypeFilter updated to: {newValue}");
            }
        }
    }

    /// <summary>
    /// Target computer name for WMI connection - used for the text box input only
    /// </summary>
    public string ComputerName
    {
        get => _temporaryComputerName;
        set => SetProperty(ref _temporaryComputerName, value);
    }

    /// <summary>
    /// Command to connect to a WMI namespace
    /// </summary>
    public ICommand ConnectCommand { get; }

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
    /// Gets or sets the operation mode for WMI operations
    /// </summary>
    public WmiOperationMode OperationMode
    {
        get => _operationMode;
        set
        {
            if (SetProperty(ref _operationMode, value))
            {
                _wmiService.OperationMode = value; // Propagate to service
            }
        }
    }

    /// <summary>
    /// Command to reload classes in the current namespace
    /// </summary>
    public ICommand ReloadClassesCommand { get; }

    /// <summary>
    /// Object to display in the property grid - could be namespace, class, or instance
    /// </summary>
    public object? SelectedObject
    {
        get => _selectedObject;
        set
        {
            if (SetProperty(ref _selectedObject, value))
            {
                // Notify that the display name has changed when the selected object changes
                OnPropertyChanged(nameof(SelectedObjectDisplayName));
            }
        }
    }

    /// <summary>
    /// Gets the display name of the currently selected object for the property grid header
    /// </summary>
    public string SelectedObjectDisplayName
    {
        get
        {
            if (_selectedObject == null)
                return "No Selection";

            if (_selectedObject is WmiNamespaceViewModel namespaceVm)
                return $"Namespace: {namespaceVm.Name}";

            if (_selectedObject is WmiClassViewModel classVm)
                return $"Class: {classVm.ClassName}";

            if (_selectedObject is WmiInstanceViewModel instanceVm)
                return $"Instance: {instanceVm.InstanceName}";

            return _selectedObject.GetType().Name;
        }
    }

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
    public WmiWatcherViewModel WatcherViewModel => _watcherViewModel!;

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
    /// Handles class type filter changes
    /// </summary>
    private void HandleClassTypeFilterChangedMessage(ClassTypeFilterChangedMessage message)
    {
        if (message == null) return;

        // Update UI if needed
        OnPropertyChanged(nameof(ClassTypeFilter));

        System.Diagnostics.Debug.WriteLine($"MainViewModel received ClassTypeFilterChanged: {_settingsService.ClassTypeFilter}");
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
    /// Handles when a class is selected to update the property grid
    /// </summary>
    private void HandleSelectedClassChangedMessage(SelectedClassChangedMessage message)
    {
        if (message?.ClassViewModel == null)
            return;

        // Update the selected object for the property grid
        SelectedObject = message.ClassViewModel.WmiClass;
    }

    /// <summary>
    /// Handles when a WMI event is selected to update the property grid
    /// </summary>
    private void HandleSelectedEventChangedMessage(SelectedEventChangedMessage message)
    {
        if (message?.WmiEvent == null)
            return;

        // Update the selected object for the property grid
        SelectedObject = message.WmiEvent;
    }

    /// <summary>
    /// Handles when an instance is selected to update the property grid
    /// </summary>
    private void HandleSelectedInstanceChangedMessage(SelectedInstanceChangedMessage message)
    {
        if (message?.InstanceViewModel == null)
            return;

        // Update the selected object for the property grid
        SelectedObject = message.InstanceViewModel.WmiInstance;
    }

    /// <summary>
    /// Handles when a namespace is selected to ensure it loads its children and updates the class count/status message
    /// </summary>
    private void HandleSelectedNamespaceChangedMessage(SelectedNamespaceChangedMessage message)
    {
        if (message?.NamespaceViewModel == null)
            return;

        // Update the selected object for the property grid
        SelectedObject = message.NamespaceViewModel.WmiNamespace;
    }

    /// <summary>
    /// Handles when a search result is selected to update the property grid
    /// </summary>
    private void HandleSelectedSearchResultChangedMessage(SelectedSearchResultChangedMessage message)
    {        // Set SelectedObject to the underlying WMI object for the property grid
        if (message?.SelectedResult != null)
        {
            if (message.SelectedResult.Class != null)
                SelectedObject = message.SelectedResult.Class;
            else if (message.SelectedResult.Method != null)
                SelectedObject = message.SelectedResult.Method;
            else if (message.SelectedResult.Property != null)
                SelectedObject = message.SelectedResult.Property;
            else
                SelectedObject = message.SelectedResult.Match;
        }
        else
        {
            SelectedObject = null;
        }
    }

    /// <summary>
    /// Handles when a WMI query result instance is selected to update the property grid
    /// </summary>
    private void HandleWmiQueryInstanceChangedMessage(WmiQueryInstanceChangedMessage message)
    {
        // Set SelectedObject to the selected WMI instance for the property grid
        SelectedObject = message.Instance;
    }
}