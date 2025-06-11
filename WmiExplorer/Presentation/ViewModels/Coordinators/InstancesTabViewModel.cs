using CommunityToolkit.Mvvm.ComponentModel;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Messages;
using WmiExplorer.Common.Models;
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

    private readonly ISelectionService _selectionService;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private MainWindowPosition _windowPosition;

    public InstancesTabViewModel(
           IMessengerService messengerService,
           ISettingsService settingsService,
           ISelectionService selectionService) : base(messengerService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _selectionService = selectionService ?? throw new ArgumentNullException(nameof(selectionService));

        // Subscribe to unified selection changes instead of individual messages
        StrongSubscribe<SelectionChangedMessage>(HandleSelectionChangedMessage);

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
    /// Handles the unified selection changed message
    /// </summary>
    private void HandleSelectionChangedMessage(SelectionChangedMessage message)
    {
        if (message?.SelectionService == null)
            return;

        var selectedObject = message.SelectionService.SelectedObject;

        switch (selectedObject)
        {
            // If a namespace is selected, update class selection
            case WmiNamespaceViewModel namespaceVm:

                if (namespaceVm.SelectedClass != SelectedClass)
                {
                    SelectedClass = namespaceVm.SelectedClass;
                }
                break;

            // If a class is selected, update the selected class
            case WmiClassViewModel classVm:
                if (classVm != SelectedClass)
                {
                    SelectedClass = classVm;
                }
                break;
        }

    }
}