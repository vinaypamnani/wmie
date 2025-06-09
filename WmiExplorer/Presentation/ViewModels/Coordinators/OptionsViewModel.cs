using System.Windows.Input;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Shared;
using WmiExplorer.Presentation.ViewModels.Items;
using WmiExplorer.Services;
using WmiExplorer.Themes;

namespace WmiExplorer.Presentation.ViewModels.Coordinators;

/// <summary>
/// Coordinator ViewModel for application options and settings.
/// Manages all options-related functionality and UI operations.
/// </summary>
public class OptionsViewModel : MessagingViewModelBase
{
    private readonly WmiNamespacePaneViewModel _namespacePaneViewModel;
    private readonly ISettingsService _settingsService;
    private string _temporaryComputerName = Environment.MachineName;
    private readonly ThemeManager _themeManager;
    private readonly IWmiService _wmiService;

    public OptionsViewModel(
        IMessagingService messagingService,
        ISettingsService settingsService,
        ThemeManager themeManager,
        IWmiService wmiService,
        WmiNamespacePaneViewModel namespacePaneViewModel)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
        _wmiService = wmiService ?? throw new ArgumentNullException(nameof(wmiService));
        _namespacePaneViewModel = namespacePaneViewModel ?? throw new ArgumentNullException(nameof(namespacePaneViewModel));

        // Initialize messaging
        InitializeMessaging(messagingService);

        // Delegate commands to coordinators
        ConnectCommand = new AsyncRelayCommand(async () => await _namespacePaneViewModel.ConnectAsync(ComputerName.Trim()));
        ReloadClassesCommand = _namespacePaneViewModel.ReloadClassesCommand;

        // ToggleThemeCommand
        ToggleThemeCommand = new RelayCommand(_ => _themeManager.ToggleTheme());

        // Subscribe to messages
        StrongSubscribe<ClassTypeFilterChangedMessage>(HandleClassTypeFilterChangedMessage);
        StrongSubscribe<ThemeChangedMessage>(HandleThemeChangedMessage);
        StrongSubscribe<SelectedNamespaceChangedMessage>(HandleSelectedNamespaceChangedMessage);


        // Subscribe to settings changes
        _settingsService.ShowSystemClassesChanged += (s, v) =>
        {
            OnPropertyChanged(nameof(ShowSystemClasses));
        };
    }

    /// <summary>
    /// Filter for WMI class types - wraps the settings service
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
    public ICommand ConnectCommand { get; private set; }

    /// <summary>
    /// Gets the current theme object
    /// </summary>
    public Theme CurrentTheme => _themeManager.CurrentThemeObject!;

    /// <summary>
    /// Gets or sets the operation mode for WMI operations
    /// </summary>
    public WmiOperationMode OperationMode
    {
        get => _wmiService.OperationMode;
        set
        {
            if (_wmiService.OperationMode != value)
            {
                _wmiService.OperationMode = value; // Update the WMI service operation mode
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Command to reload classes in the current namespace
    /// </summary>
    public ICommand ReloadClassesCommand { get; private set; }

    /// <summary>
    /// Gets the currently selected namespace for enabling/disabling reload command
    /// </summary>
    public WmiNamespaceViewModel? SelectedNamespace => _namespacePaneViewModel.SelectedNamespace;

    /// <summary>
    /// Flag indicating whether system classes should be shown
    /// </summary>
    public bool ShowSystemClasses
    {
        get => _namespacePaneViewModel.ClassesTabViewModel.ShowSystemClasses;
        set => _namespacePaneViewModel.ClassesTabViewModel.ShowSystemClasses = value;
    }

    /// <summary>
    /// Command to toggle between light and dark theme
    /// </summary>
    public ICommand ToggleThemeCommand { get; private set; }

    /// <summary>
    /// Handles class type filter changes
    /// </summary>
    private void HandleClassTypeFilterChangedMessage(ClassTypeFilterChangedMessage message)
    {
        if (message == null) return;

        // Update UI if needed
        OnPropertyChanged(nameof(ClassTypeFilter));

        System.Diagnostics.Debug.WriteLine($"OptionsViewModel received ClassTypeFilterChanged: {_settingsService.ClassTypeFilter}");
    }

    /// <summary>
    /// Handles when the selected namespace changes
    /// </summary>
    private void HandleSelectedNamespaceChangedMessage(SelectedNamespaceChangedMessage message)
    {
        // Notify the UI that SelectedNamespace property has changed
        OnPropertyChanged(nameof(SelectedNamespace));
    }

    /// <summary>
    /// Handles theme change messages
    /// </summary>
    private void HandleThemeChangedMessage(ThemeChangedMessage message)
    {
        OnPropertyChanged(nameof(CurrentTheme));
    }
}