using CommunityToolkit.Mvvm.ComponentModel;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Messages;
using WmiExplorer.Common.Models;
using WmiExplorer.Core.Models;
using WmiExplorer.Presentation.ViewModels.Items;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Coordinators;

/// <summary>
/// Coordinator ViewModel for the PropertyGrid functionality.
/// Manages property grid operations and selected object display.
/// </summary>
public partial class PropertyGridViewModel : MessagingViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedObjectDisplayName))]
    private object? _selectedObject;

    private readonly ISelectionService _selectionService;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private MainWindowPosition _windowPosition;

    public PropertyGridViewModel(
           IMessengerService messengerService,
           ISettingsService settingsService,
           ISelectionService selectionService) : base(messengerService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _selectionService = selectionService ?? throw new ArgumentNullException(nameof(selectionService));

        // Initialize window position from settings (following the pattern)
        _windowPosition = _settingsService.MainWindowPosition;

        // Subscribe to the unified selection change message
        StrongSubscribe<SelectionChangedMessage>(HandleSelectionChangedMessage);
    }

    /// <summary>
    /// Gets the display name of the currently selected object for the property grid header
    /// </summary>
    public string SelectedObjectDisplayName => _selectionService.SelectedObjectDisplayName;

    /// <summary>
    /// Handles the unified selection changed message to update the property grid
    /// </summary>
    private void HandleSelectionChangedMessage(SelectionChangedMessage message)
    {
        if (message?.SelectionService == null)
            return;

        //TODO: Add a debug option here to see raw message.SelectionService.SelectedObject

        // Get the selected object from the selection service
        var selectedObject = message.SelectionService.SelectedObject;
        switch (selectedObject)
        {
            case WmiNamespaceViewModel namespaceViewModel:
                selectedObject = namespaceViewModel.WmiNamespace;
                break;
            case WmiClassViewModel classViewModel:
                selectedObject = classViewModel.WmiClass;
                break;
            case WmiInstanceViewModel instanceViewModel:
                selectedObject = instanceViewModel.WmiInstance;
                break;
            case WmiSearchResult wmiSearchResult:
                if (wmiSearchResult.Class is WmiClass wmiClass)
                    selectedObject = wmiClass;
                else if (wmiSearchResult.Method is WmiMethod wmiMethod)
                    selectedObject = wmiMethod;
                else if (wmiSearchResult.Property is WmiProperty wmiProperty)
                    selectedObject = wmiProperty;
                else
                    selectedObject = wmiSearchResult.Match;
                break;
            default:
                break;
        }

        // Update the selected object for the property grid from the selection service
        SelectedObject = selectedObject;
    }
}