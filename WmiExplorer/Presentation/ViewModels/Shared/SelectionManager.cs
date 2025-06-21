using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows;
using WmiExplorer.Common.Messages;
using WmiExplorer.Core.Models;
using WmiExplorer.Presentation.ViewModels.Items;
using WmiExplorer.Presentation.ViewModels.Watcher;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Shared;

/// <summary>
/// Manages application-wide selection state and PropertyGrid updates.
/// Focuses on maintaining SelectedNamespace as the primary selection state,
/// with SelectedClass and SelectedInstance as convenience properties that
/// delegate to the namespace's internal selection properties.
/// This simplified approach eliminates complex synchronization logic since
/// the UI is bound to SelectionManager.SelectedNamespace.* properties.
/// </summary>
public partial class SelectionManager : ObservableObject
{
    private const string NoSelectionDisplayName = "No Selection";

    [ObservableProperty]
    private DateTime _lastUpdateTime = DateTime.Now;

    private readonly IMessengerService _messengerService;

    // Primary selection property that the UI binds to
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedClass), nameof(SelectedInstance))]
    private WmiNamespaceViewModel? _selectedNamespace;

    [ObservableProperty]
    private string _selectedObjectDisplayName = NoSelectionDisplayName;

    // PropertyGrid binding properties
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedObjectDisplayName))]
    private object? _selectedObjectForPropertyGrid;

    public SelectionManager(IMessengerService messengerService)
    {
        _messengerService = messengerService ?? throw new ArgumentNullException(nameof(messengerService));
    }

    public object? PreviousObject { get; private set; }

    // Convenience properties that delegate to SelectedNamespace
    public WmiClassViewModel? SelectedClass => SelectedNamespace?.SelectedClass;

    public WmiInstanceViewModel? SelectedInstance => SelectedNamespace?.SelectedClass?.SelectedInstance;

    // Selection state for coordination between ViewModels
    public object? SelectedObject { get; private set; }

    /// <summary>
    /// Clears only the PropertyGrid without affecting the selection state.
    /// This is useful when we want to hide PropertyGrid content while keeping
    /// the current selection for coordination between ViewModels.
    /// </summary>
    public void ClearPropertyGrid()
    {
        SelectedObjectForPropertyGrid = null;
        SelectedObjectDisplayName = NoSelectionDisplayName;
        LastUpdateTime = DateTime.Now;

        // Note: We don't clear SelectedObject or send SelectionChangedMessage here
        // This is intentional - we only want to clear the PropertyGrid display
    }

    /// <summary>
    /// Clears both selection state and PropertyGrid
    /// </summary>
    public void ClearSelections()
    {
        PreviousObject = SelectedObject;
        SelectedObject = null;

        SelectedObjectForPropertyGrid = null;
        SelectedObjectDisplayName = NoSelectionDisplayName;
        LastUpdateTime = DateTime.Now;

        // Clear the primary selection property - other properties are derived
        SelectedNamespace = null;

        PublishSelectionChanged();
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
            await Task.Delay(10);

            // Restore the selection on the UI thread
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                SelectedObjectForPropertyGrid = currentObject;
                SelectedObjectDisplayName = currentDisplayName;
                LastUpdateTime = DateTime.Now;
            });
        });
    }

    /// <summary>
    /// Sets the PropertyGrid object directly without changing the selected object state.
    /// This is useful when we want to display something in the PropertyGrid while keeping
    /// the selection state separate from the PropertyGrid display.
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

        // Note: We don't update SelectedObject or send SelectionChangedMessage here
        // This is intentional - we only want to update the PropertyGrid display
    }

    /// <summary>
    /// Sets the selected object and optionally updates the PropertyGrid.
    /// This method can handle the same object being selected multiple times
    /// when updatePropertyGrid is true.
    /// </summary>
    /// <param name="selectedObject">The object to select</param>
    /// <param name="updatePropertyGrid">Whether to update the PropertyGrid (with force refresh)</param>
    public void SetSelectedObject(object? selectedObject, bool updatePropertyGrid = true)
    {
        // Always update selection state for coordination
        PreviousObject = SelectedObject;
        SelectedObject = ProcessSelectionObject(selectedObject);

        // Update centralized selection properties
        UpdateCentralizedSelectionProperties(selectedObject);

        // Send SelectionChangedMessage for ViewModels that need it
        PublishSelectionChanged();

        // Optionally update PropertyGrid (always refresh when requested)
        if (updatePropertyGrid)
        {
            var (processedObject, displayName) = ProcessPropertyGridObject(selectedObject);
            SelectedObjectForPropertyGrid = processedObject;
            SelectedObjectDisplayName = displayName;
            LastUpdateTime = DateTime.Now;
        }
    }

    // private void OnClassPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    // {
    //     if (e.PropertyName == nameof(WmiClassViewModel.SelectedInstance))
    //     {
    //         // Notify that SelectedInstance changed
    //         OnPropertyChanged(nameof(SelectedInstance));
    //     }
    // }

    // private void OnNamespacePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    // {
    //     if (e.PropertyName == nameof(WmiNamespaceViewModel.SelectedClass))
    //     {
    //         // Unsubscribe from old class
    //         var oldClass = SelectedClass;
    //         if (oldClass != null)
    //             oldClass.PropertyChanged -= OnClassPropertyChanged;

    //         // Notify that SelectedClass changed
    //         OnPropertyChanged(nameof(SelectedClass));
    //         OnPropertyChanged(nameof(SelectedInstance));

    //         // Subscribe to new class
    //         var newClass = SelectedNamespace?.SelectedClass;
    //         if (newClass != null)
    //             newClass.PropertyChanged += OnClassPropertyChanged;
    //     }
    // }

    // partial void OnSelectedNamespaceChanged(WmiNamespaceViewModel? oldValue, WmiNamespaceViewModel? newValue)
    // {
    //     // Unsubscribe from old namespace events
    //     if (oldValue != null)
    //     {
    //         oldValue.PropertyChanged -= OnNamespacePropertyChanged;
    //         if (oldValue.SelectedClass != null)
    //             oldValue.SelectedClass.PropertyChanged -= OnClassPropertyChanged;
    //     }

    //     // Subscribe to new namespace events
    //     if (newValue != null)
    //     {
    //         newValue.PropertyChanged += OnNamespacePropertyChanged;
    //         if (newValue.SelectedClass != null)
    //             newValue.SelectedClass.PropertyChanged += OnClassPropertyChanged;
    //     }
    // }

    /// <summary>
    /// Processes the selected object for PropertyGrid display and generates display name
    /// </summary>
    private (object? processedObject, string displayName) ProcessPropertyGridObject(object? selectedObject)
    {
        object? processedObject;
        string displayName; switch (selectedObject)
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

    /// <summary>
    /// Processes the selected object for selection coordination (maintains original logic)
    /// </summary>
    private object? ProcessSelectionObject(object? selectedObject)
    {
        return selectedObject switch
        {
            WmiNamespaceViewModel namespaceViewModel => namespaceViewModel,
            WmiClassViewModel classViewModel => classViewModel,
            WmiInstanceViewModel instanceViewModel => instanceViewModel,
            WmiEvent wmiEvent => wmiEvent,
            WmiSearchResult searchResult => searchResult,
            WmiInstance queryInstance => queryInstance,
            null => null,
            _ => selectedObject
        };
    }

    private void PublishSelectionChanged()
    {
        _messengerService.Send(new SelectionChangedMessage(this));
    }

    /// <summary>
    /// Updates the selected namespace based on the selected object.
    /// All other selection properties are derived from SelectedNamespace.
    /// </summary>
    private void UpdateCentralizedSelectionProperties(object? selectedObject)
    {
        switch (selectedObject)
        {
            case WmiNamespaceViewModel namespaceVm:
                // Direct namespace selection
                if (SelectedNamespace != namespaceVm)
                    SelectedNamespace = namespaceVm;
                break;

            case WmiClassViewModel classVm:

                // Update the namespace's selected class
                if (SelectedNamespace?.SelectedClass != classVm)
                    SelectedNamespace!.SelectedClass = classVm;
                break;

            case WmiInstanceViewModel instanceVm:

                // Force the instance to try to get its data
                instanceVm?.TryGetInstance();

                // Update the class's selected instance
                if (SelectedNamespace?.SelectedClass?.SelectedInstance != instanceVm)
                    SelectedNamespace!.SelectedClass!.SelectedInstance = instanceVm;
                break;

            default:
                // For non-WMI objects, we don't change the namespace selection
                break;
        }
    }
}