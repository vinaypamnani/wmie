using CommunityToolkit.Mvvm.ComponentModel;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Models;
using WmiExplorer.Common.Messages;
using WmiExplorer.Presentation.ViewModels.Items;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Coordinators;

/// <summary>
/// Coordinator ViewModel for the WMI Instances tab. Manages instance-related functionality
/// and UI operations for the instances list view.
/// </summary>
public partial class InstancesTabViewModel : MessagingViewModelBase
{
    private readonly CancellationTokenSource _cts = new();

    [ObservableProperty]
    private WmiClassViewModel? _selectedClass;

    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private MainWindowPosition _windowPosition;

    public InstancesTabViewModel(
        IMessengerService messengerService,
        ISettingsService settingsService) : base(messengerService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        // Subscribe to messages
        StrongSubscribe<SelectedClassChangedMessage>(HandleSelectedClassChangedMessage);

        // Initialize window position from settings
        _windowPosition = _settingsService.MainWindowPosition;
    }

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