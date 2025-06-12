using WmiExplorer.Common.Messages;
using WmiExplorer.Core.Models;
using WmiExplorer.Presentation.ViewModels.Items;

namespace WmiExplorer.Services;

/// <summary>
/// Service for managing application-wide selection state
/// </summary>
public class SelectionService : ISelectionService
{
    private readonly IMessengerService _messengerService;

    public SelectionService(IMessengerService messengerService)
    {
        _messengerService = messengerService ?? throw new ArgumentNullException(nameof(messengerService));
    }

    public object? PreviousObject { get; private set; }
    public object? SelectedObject { get; private set; }

    public void ClearSelections()
    {
        PreviousObject = SelectedObject;
        SelectedObject = null;
        PublishSelectionChanged();
    }

    public void SetSelectedObject(object? selectedObject)
    {
        // Store the current selection as previous before setting new one
        PreviousObject = SelectedObject;

        // Set the appropriate selection based on object type
        switch (selectedObject)
        {
            case WmiNamespaceViewModel namespaceViewModel:
                SelectedObject = namespaceViewModel;
                break;

            case WmiClassViewModel classViewModel:
                SelectedObject = classViewModel;
                break;

            case WmiInstanceViewModel instanceViewModel:
                SelectedObject = instanceViewModel;
                break;

            case WmiEvent wmiEvent:
                SelectedObject = wmiEvent;
                break;

            case WmiSearchResult searchResult:
                SelectedObject = searchResult;
                break;

            case WmiInstance queryInstance:
                SelectedObject = queryInstance;
                break;

            case null:
                SelectedObject = null;
                break;

            default:
                // For any other objects, set them directly
                SelectedObject = selectedObject;
                break;
        }

        PublishSelectionChanged();
    }

    private void PublishSelectionChanged()
    {
        _messengerService.Send(new SelectionChangedMessage(this));
    }
}