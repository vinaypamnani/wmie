using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Management;
using System.Windows;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Messages;
using WmiExplorer.Presentation.Themes;
using WmiExplorer.Presentation.ViewModels.Shared;
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
    private readonly SettingsManager _settingsManager;
    private readonly ThemeManager _themeManager;

    public OptionsViewModel(
           IMessengerService messengerService,
           SettingsManager settingsManager,
           ThemeManager themeManager,
           NamespacesViewModel namespacesViewModel) : base(messengerService)
    {
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
        _namespacesViewModel = namespacesViewModel ?? throw new ArgumentNullException(nameof(namespacesViewModel));

        // Subscribe to messages
        StrongSubscribe<ThemeChangedMessage>(HandleThemeChangedMessage);

        // Subscribe to command state changes
        SubscribeToCommandStateChanges();
    }

    /// <summary>
    /// Gets the current theme object
    /// </summary>
    public Theme CurrentTheme => _themeManager.CurrentTheme!;

    public SettingsManager SettingsManager => _settingsManager;

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
        _namespacesViewModel.SelectionManager.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_namespacesViewModel.SelectionManager.SelectedNamespace))
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
        _themeManager.ToggleTheme();
    }
}