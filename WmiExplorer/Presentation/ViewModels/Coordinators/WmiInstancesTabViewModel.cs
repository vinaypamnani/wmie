using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using System.Windows.Input;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Shared;
using WmiExplorer.Presentation.ViewModels.Items;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Coordinators;

/// <summary>
/// Coordinator ViewModel for the WMI Instances tab. Manages instance-related functionality
/// and UI operations for the instances list view.
/// </summary>
public partial class WmiInstancesTabViewModel : MessagingViewModel
{
    private readonly IApplicationService _applicationService;
    private readonly ICacheService _cacheService;
    private readonly CancellationTokenSource _cts = new();

    [ObservableProperty]
    private WmiClassViewModel? _selectedClass;

    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private MainWindowPosition _windowPosition;

    private readonly IWmiService _wmiService;

    public WmiInstancesTabViewModel(
        IMessenger messenger,
        ISettingsService settingsService,
        IWmiService wmiService,
        IApplicationService applicationService,
        ICacheService cacheService) : base(messenger)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _wmiService = wmiService ?? throw new ArgumentNullException(nameof(wmiService));
        _applicationService = applicationService ?? throw new ArgumentNullException(nameof(applicationService));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));

        // Subscribe to messages
        StrongSubscribe<SelectedClassChangedMessage>(HandleSelectedClassChangedMessage);

        // Initialize window position from settings
        _windowPosition = _settingsService.MainWindowPosition;

        // Initialize command
        LoadInstancesCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(
            () => SelectedClass?.LoadInstancesCommand.Execute(null),
            () => SelectedClass != null && SelectedClass.LoadInstancesCommand.CanExecute(null)
        );
    }

    /// <summary>
    /// Command to load instances for the selected class
    /// </summary>
    public ICommand LoadInstancesCommand { get; }

    /// <summary>
    /// Cleanup resources on disposal
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cts.Cancel();
            _cts.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// Handles the SelectedClassChangedMessage
    /// </summary>
    private void HandleSelectedClassChangedMessage(SelectedClassChangedMessage message)
    {
        SelectedClass = message.ClassViewModel;
    }
}