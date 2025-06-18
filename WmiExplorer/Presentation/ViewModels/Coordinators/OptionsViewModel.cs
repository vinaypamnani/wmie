using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Management;
using System.Windows;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Enums;
using WmiExplorer.Common.Messages;
using WmiExplorer.Presentation.Themes;
using WmiExplorer.Presentation.Views.Dialogs;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Coordinators;

/// <summary>
/// Coordinator ViewModel for application options and settings.
/// Manages all options-related functionality and UI operations.
/// </summary>
public partial class OptionsViewModel : MessagingViewModelBase
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReloadClassesCommand))]
    private WmiClassEnumerationFlags _classTypeFilter;

    [ObservableProperty]
    private string _computerName = Environment.MachineName;

    /// <summary>
    /// Connection options for WMI connections. Updated when ConnectAs is used.
    /// </summary>
    [ObservableProperty]
    private ConnectionOptions _connectionOptions = new ConnectionOptions
    {
        EnablePrivileges = true,
        Impersonation = ImpersonationLevel.Impersonate,
        Authentication = AuthenticationLevel.Default,
        Username = null,
        SecurePassword = null
    };

    private readonly NamespacesViewModel _namespacesViewModel;

    [ObservableProperty]
    private WmiOperationMode _operationMode;

    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;
    private readonly IWmiService _wmiService;

    public OptionsViewModel(
           IMessengerService messengerService,
           ISettingsService settingsService,
           IThemeService themeService,
           IWmiService wmiService,
           NamespacesViewModel namespacesViewModel) : base(messengerService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _wmiService = wmiService ?? throw new ArgumentNullException(nameof(wmiService));
        _namespacesViewModel = namespacesViewModel ?? throw new ArgumentNullException(nameof(namespacesViewModel));

        // Initialize the operation mode from the service
        _operationMode = _wmiService.OperationMode;

        // Initialize the class type filter from the settings
        _classTypeFilter = _settingsService.ClassEnumerationFilter;

        // Subscribe to messages
        StrongSubscribe<ThemeChangedMessage>(HandleThemeChangedMessage);

        // Subscribe to command state changes
        SubscribeToCommandStateChanges();
    }

    /// <summary>
    /// Gets the current theme object
    /// </summary>
    public Theme CurrentTheme => _themeService.CurrentTheme!;

    /// <summary>
    /// Command to connect to a WMI namespace
    /// </summary>
    [RelayCommand]
    private async Task ConnectAsync()
    {
        await _namespacesViewModel.ConnectAsync(ComputerName.Trim(), ConnectionOptions);
    }

    /// <summary>
    /// Command to connect to a WMI namespace with custom connection options
    /// </summary>
    [RelayCommand]
    private async Task ConnectWithOptionsAsync()
    {
        // Show the connection options dialog with current options pre-populated
        var mainWindow = Application.Current.MainWindow;
        var dialog = new ConnectionOptionsDialog(mainWindow, ConnectionOptions, ComputerName);
        var result = dialog.ShowDialog();

        if (result == true && dialog.Result != null)
        {
            // Update our stored connection options
            ConnectionOptions = dialog.Result;

            // Update the computer name if it was changed in the dialog
            if (!string.IsNullOrWhiteSpace(dialog.ComputerNameResult))
            {
                ComputerName = dialog.ComputerNameResult;
            }

            // Connect using the new connection options and computer name
            await _namespacesViewModel.ConnectAsync(ComputerName.Trim(), ConnectionOptions);
        }
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
    partial void OnClassTypeFilterChanged(WmiClassEnumerationFlags value)
    {
        // Process the value for flag operations
        var currentValue = _settingsService.ClassEnumerationFilter;
        var newValue = value;

        // Check if the incoming value is actually a negative flag value from our converter
        // Negative values indicate a flag needs to be cleared
        if ((int)value < 0)
        {
            // This is a signal from our converter that we need to clear a flag
            // Convert the negative value back to a positive flag by taking its complement again
            var flagToClear = (WmiClassEnumerationFlags)(~(int)value);

            // Clear the specific flag while preserving all other flags
            newValue = currentValue & ~flagToClear;

            // Update the property with the processed value (avoiding infinite recursion)
            _classTypeFilter = newValue;

            // Also update the service immediately
            _settingsService.ClassEnumerationFilter = newValue;

            // Publish the message to notify other components
            PublishMessage(new ClassEnumFilterChangedMessage(newValue));

            return;
        }
        else if ((int)value > 0 && (int)value <= (int)WmiClassEnumerationFlags.All)
        {
            // This is a positive flag value coming from the converter when a checkbox is checked
            // Set this flag while preserving all other flags
            newValue = currentValue | value;

            // If we're setting a compound value, update the property (avoiding infinite recursion)
            if (newValue != value)
            {
                _classTypeFilter = newValue;

                // Also update the service immediately
                _settingsService.ClassEnumerationFilter = newValue;

                // Publish the message to notify other components
                PublishMessage(new ClassEnumFilterChangedMessage(newValue));

                return;
            }
        }        // Only update the service if the value is different
        if (_settingsService.ClassEnumerationFilter != newValue)
        {
            // Update the setting without triggering notifications from the service
            _settingsService.ClassEnumerationFilter = newValue;

            // Publish the message ourselves to notify other components
            PublishMessage(new ClassEnumFilterChangedMessage(newValue));
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
    /// Command to reload classes in the current namespace
    /// </summary>
    [RelayCommand(CanExecute = nameof(ReloadClassesCanExecute))]
    private void ReloadClasses()
    {
        _namespacesViewModel.ReloadClassesCommand.Execute(null);
    }

    private bool ReloadClassesCanExecute() => _namespacesViewModel.ReloadClassesCommand.CanExecute(null);

    /// <summary>
    /// Subscribe to changes that affect the command's CanExecute state
    /// </summary>
    private void SubscribeToCommandStateChanges()
    {
        _namespacesViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_namespacesViewModel.SelectedNamespace))
            {
                // Notify that the command's CanExecute state may have changed
                ReloadClassesCommand.NotifyCanExecuteChanged();
            }
        };
    }

    /// <summary>
    /// Command to toggle between light and dark theme
    /// </summary>
    [RelayCommand]
    private void ToggleTheme()
    {
        _themeService.ToggleTheme();
    }
}