using CommunityToolkit.Mvvm.ComponentModel;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Models;
using WmiExplorer.Presentation.ViewModels.Shared;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Coordinators;

/// <summary>
/// Coordinator ViewModel for the WMI Instances tab. Manages instance-related functionality
/// and UI operations for the instances list view.
/// </summary>
public partial class InstancesTabViewModel : SelectionAwareViewModelBase
{
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private MainWindowPosition _windowPosition;

    public InstancesTabViewModel(
                    IMessengerService messengerService,
                    ISettingsService settingsService,
                    SelectionManager selectionManager) : base(messengerService, selectionManager)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        // Initialize window position from settings
        _windowPosition = _settingsService.MainWindowPosition;
    }
}