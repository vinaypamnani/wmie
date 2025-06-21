using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows;
using WmiExplorer.Core.Models;
using WmiExplorer.Presentation.ViewModels.Items;
using WmiExplorer.Presentation.ViewModels.Watcher;

namespace WmiExplorer.Presentation.ViewModels.Shared;

/// <summary>
/// Manages PropertyGrid display and updates.
/// Handles object processing, display name generation, and PropertyGrid state management.
/// Works in coordination with SelectionManager to provide PropertyGrid functionality.
/// </summary>
public partial class PropertyGridManager : ObservableObject
{
    private const string NoSelectionDisplayName = "No Selection";

    [ObservableProperty]
    private DateTime _lastUpdateTime = DateTime.Now;

    [ObservableProperty]
    private string _selectedObjectDisplayName = NoSelectionDisplayName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedObjectDisplayName))]
    private object? _selectedObjectForPropertyGrid;

    /// <summary>
    /// Clears only the PropertyGrid without affecting any selection state.
    /// </summary>
    public void ClearPropertyGrid()
    {
        SelectedObjectForPropertyGrid = null;
        SelectedObjectDisplayName = NoSelectionDisplayName;
        LastUpdateTime = DateTime.Now;
    }

    /// <summary>
    /// Forces a PropertyGrid refresh by temporarily clearing and resetting the object.
    /// This is useful when the underlying object's properties have been modified.
    /// </summary>
    public void RefreshPropertyGrid()
    {
        if (SelectedObjectForPropertyGrid == null) return;

        var currentObject = SelectedObjectForPropertyGrid;
        var currentDisplayName = SelectedObjectDisplayName;

        // Temporarily clear the selection to force refresh
        SelectedObjectForPropertyGrid = null;
        SelectedObjectDisplayName = NoSelectionDisplayName;

        // Use a small delay to ensure the PropertyGrid processes the null value
        Task.Run(async () =>
        {
            // Restore the selection on the UI thread
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Task.Delay(10);
                SelectedObjectForPropertyGrid = currentObject;
                SelectedObjectDisplayName = currentDisplayName;
                LastUpdateTime = DateTime.Now;
            });
        });
    }

    /// <summary>
    /// Sets the PropertyGrid object directly without affecting any selection state.
    /// This is useful when we want to display something in the PropertyGrid independently
    /// of the current selection.
    /// </summary>
    /// <param name="propertyGridObject">The object to display in the PropertyGrid</param>
    /// <param name="displayName">Optional custom display name for the object</param>
    public void SetPropertyGridObject(object? propertyGridObject, string? displayName = null)
    {
        // Process the object for PropertyGrid display
        var (processedObject, generatedDisplayName) = ProcessPropertyGridObject(propertyGridObject);

        // Use custom display name if provided, otherwise use the generated one
        var finalDisplayName = displayName ?? generatedDisplayName;

        SelectedObjectForPropertyGrid = processedObject;
        SelectedObjectDisplayName = finalDisplayName;
        LastUpdateTime = DateTime.Now;
    }

    /// <summary>
    /// Updates the PropertyGrid with a selected object, processing it appropriately for display.
    /// </summary>
    /// <param name="selectedObject">The object to display in the PropertyGrid</param>
    public void UpdatePropertyGridFromSelection(object? selectedObject)
    {
        // Delegate to SetPropertyGridObject with no custom display name (uses generated name)
        SetPropertyGridObject(selectedObject, displayName: null);
    }

    /// <summary>
    /// Processes objects for PropertyGrid display, extracting the appropriate underlying objects
    /// and generating descriptive display names.
    /// </summary>
    private (object? processedObject, string displayName) ProcessPropertyGridObject(object? selectedObject)
    {
        object? processedObject;
        string displayName;

        switch (selectedObject)
        {
            case WmiNamespaceViewModel namespaceViewModel:
                processedObject = namespaceViewModel.WmiNamespace;
                displayName = processedObject?.ToString() ?? NoSelectionDisplayName;
                break;

            case WmiClassViewModel classViewModel:
                processedObject = classViewModel.WmiClass;
                displayName = processedObject?.ToString() ?? NoSelectionDisplayName;
                break;

            case WmiInstanceViewModel instanceViewModel:
                processedObject = instanceViewModel.WmiInstance;
                displayName = processedObject?.ToString() ?? NoSelectionDisplayName;
                break;

            case WmiEventWatcherViewModel eventWatcherViewModel:
                processedObject = eventWatcherViewModel.Watcher;
                displayName = $"Event Watcher: {eventWatcherViewModel.Name}";
                break;

            case WmiMethod wmiMethod:
                processedObject = wmiMethod;
                displayName = $"Method: {wmiMethod.Name} ({processedObject})";
                break;

            case WmiSearchResult wmiSearchResult:
                displayName = wmiSearchResult?.ToString() ?? NoSelectionDisplayName;
                if (wmiSearchResult?.Class is WmiClass wmiClass)
                    processedObject = wmiClass;
                else if (wmiSearchResult?.Method is WmiMethod wmiMethod2)
                    processedObject = wmiMethod2;
                else if (wmiSearchResult?.Property is WmiProperty wmiProperty)
                    processedObject = wmiProperty;
                else
                    processedObject = wmiSearchResult?.Match;
                break;

            case null:
                processedObject = null;
                displayName = NoSelectionDisplayName;
                break;

            default:
                processedObject = selectedObject;
                displayName = selectedObject?.ToString() ?? NoSelectionDisplayName;
                break;
        }

        return (processedObject, displayName);
    }
}