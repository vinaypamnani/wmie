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

    public object? SelectedObject { get; private set; }

    public string SelectedObjectDisplayName
    {
        get
        {
            if (SelectedObject == null)
                return "No Selection";

            return SelectedObject switch
            {
                WmiNamespaceViewModel nsVm => $"Namespace: {nsVm.Name}",
                WmiClassViewModel clsVm => $"Class: {clsVm.ClassName}",
                WmiClass cls => $"Class: {cls.ClassName}",
                WmiInstanceViewModel instVm => $"Instance: {instVm.InstanceName}",
                WmiInstance inst => $"Instance: {inst.InstanceName}",
                WmiProperty prop => $"Property: {prop.Name}",
                WmiMethod method => $"Method: {method.Name}",
                WmiEvent evt => $"Event: {evt.EventDisplayPropertyValue}",
                WmiSearchResult result => $"Search Result: {result.Name}",
                _ => SelectedObject.GetType().Name
            };
        }
    }

    public void ClearSelections()
    {
        SelectedObject = null;
        PublishSelectionChanged();
    }

    public void SetSelectedObject(object? selectedObject)
    {
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