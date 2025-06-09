using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Shared;
using WmiExplorer.Services;
using WmiExplorer.Themes;

namespace WmiExplorer.Presentation.ViewModels.Coordinators;

/// <summary>
/// Coordinator ViewModel for application options and settings.
/// Manages all options-related functionality and UI operations.
/// </summary>
public partial class OptionsViewModel : MessagingViewModel
{
    [ObservableProperty]
    private WmiClassTypeFlags _classTypeFilter;

    [ObservableProperty]
    private string _computerName = Environment.MachineName;

    private readonly WmiNamespacePaneViewModel _namespacePaneViewModel;

    [ObservableProperty]
    private WmiOperationMode _operationMode;

    private ICommand? _reloadClassesCommand;
    private readonly ISettingsService _settingsService;
    private readonly ThemeManager _themeManager;
    private readonly IWmiService _wmiService;

    public OptionsViewModel(
           IMessengerService messengerService,
           ISettingsService settingsService,
           ThemeManager themeManager,
           IWmiService wmiService,
           WmiNamespacePaneViewModel namespacePaneViewModel) : base(messengerService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
        _wmiService = wmiService ?? throw new ArgumentNullException(nameof(wmiService));
        _namespacePaneViewModel = namespacePaneViewModel ?? throw new ArgumentNullException(nameof(namespacePaneViewModel));

        // Initialize the operation mode from the service
        _operationMode = _wmiService.OperationMode;

        // Initialize the class type filter from the settings
        _classTypeFilter = _settingsService.ClassTypeFilter;

        // Subscribe to messages
        StrongSubscribe<ThemeChangedMessage>(HandleThemeChangedMessage);

        // Subscribe to command state changes
        SubscribeToCommandStateChanges();
    }

    /// <summary>
    /// Gets the current theme object
    /// </summary>
    public Theme CurrentTheme => _themeManager.CurrentThemeObject!;

    /// <summary>
    /// Command to reload classes in the current namespace
    /// </summary>
    public ICommand ReloadClassesCommand => _reloadClassesCommand ??= new RelayCommand(
        ExecuteReloadClasses,
        CanExecuteReloadClasses
    );

    /// <summary>
    /// Determines if the reload classes command can be executed
    /// </summary>
    private bool CanExecuteReloadClasses()
    {
        return _namespacePaneViewModel.ReloadClassesCommand.CanExecute(null);
    }

    /// <summary>
    /// Command to connect to a WMI namespace
    /// </summary>
    [RelayCommand()]
    private async Task ConnectAsync()
    {
        await _namespacePaneViewModel.ConnectAsync(ComputerName.Trim());
    }

    /// <summary>
    /// Executes the reload classes command
    /// </summary>
    private void ExecuteReloadClasses()
    {
        _namespacePaneViewModel.ReloadClassesCommand.Execute(null);
    }

    /// <summary>
    /// Handles theme change messages
    /// </summary>
    private void HandleThemeChangedMessage(ThemeChangedMessage message)
    {
        // Notify the UI that the CurrentTheme property has changed
        OnPropertyChanged(nameof(CurrentTheme));
    }

    // Partial method that will be called when ClassTypeFilter changes
    partial void OnClassTypeFilterChanged(WmiClassTypeFlags value)
    {
        // Process the value for flag operations
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

            // Update the property with the processed value (avoiding infinite recursion)
            _classTypeFilter = newValue;

            // Also update the service immediately
            _settingsService.ClassTypeFilter = newValue;

            // Publish the message to notify other components
            PublishMessage(new ClassTypeFilterChangedMessage(newValue));

            System.Diagnostics.Debug.WriteLine($"ClassTypeFilter flag cleared, new value: {newValue}");
            return;
        }
        else if ((int)value > 0 && (int)value <= (int)WmiClassTypeFlags.All)
        {
            // This is a positive flag value coming from the converter when a checkbox is checked
            // Set this flag while preserving all other flags
            newValue = currentValue | value;

            // If we're setting a compound value, update the property (avoiding infinite recursion)
            if (newValue != value)
            {
                _classTypeFilter = newValue;

                // Also update the service immediately
                _settingsService.ClassTypeFilter = newValue;

                // Publish the message to notify other components
                PublishMessage(new ClassTypeFilterChangedMessage(newValue));

                System.Diagnostics.Debug.WriteLine($"ClassTypeFilter flag set, new value: {newValue}");
                return;
            }
        }        // Only update the service if the value is different
        if (_settingsService.ClassTypeFilter != newValue)
        {
            // Update the setting without triggering notifications from the service
            _settingsService.ClassTypeFilter = newValue;

            // Publish the message ourselves to notify other components
            PublishMessage(new ClassTypeFilterChangedMessage(newValue));

            System.Diagnostics.Debug.WriteLine($"ClassTypeFilter updated to: {newValue}");
        }
    }

    // Partial method that will be called when OperationMode changes
    partial void OnOperationModeChanged(WmiOperationMode value)
    {
        // Sync the value with the WMI service
        if (_wmiService.OperationMode != value)
        {
            _wmiService.OperationMode = value;
        }
    }

    /// <summary>
    /// Subscribe to changes that affect the command's CanExecute state
    /// </summary>
    private void SubscribeToCommandStateChanges()
    {
        _namespacePaneViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_namespacePaneViewModel.SelectedNamespace))
            {
                // Notify that the command's CanExecute state may have changed
                ((RelayCommand)ReloadClassesCommand).NotifyCanExecuteChanged();
            }
        };
    }

    /// <summary>
    /// Command to toggle between light and dark theme
    /// </summary>
    [RelayCommand]
    private void ToggleTheme()
    {
        _themeManager.ToggleTheme();
    }
}