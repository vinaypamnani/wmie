using CommunityToolkit.Mvvm.ComponentModel;
using WmiExplorer.Common.Logging;
using WmiExplorer.Common.Messages;
using WmiExplorer.Presentation.ViewModels.Items;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels.Shared;

/// <summary>
/// Manages application-wide selection state and coordination between ViewModels.
/// Focuses on maintaining SelectedNamespace as the primary selection state,
/// with SelectedClass and SelectedInstance as convenience properties that
/// delegate to the namespace's internal selection properties.
/// Works in coordination with PropertyGridManager to ensure proper operation order:
/// selection operations always happen before PropertyGrid updates.
/// </summary>
public partial class SelectionManager : ObservableObject
{
    private readonly IMessengerService _messengerService;

    // Track previous selections per ViewModel type to enable proper IsSelected clearing
    private readonly Dictionary<Type, object?> _previousSelectionsByType = new();

    private readonly PropertyGridManager _propertyGrid;

    // Primary selection property that the UI binds to
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedClass), nameof(SelectedInstance))]
    private WmiNamespaceViewModel? _selectedNamespace;

    public SelectionManager(IMessengerService messengerService, PropertyGridManager propertyGridManager)
    {
        _messengerService = messengerService ?? throw new ArgumentNullException(nameof(messengerService));
        _propertyGrid = propertyGridManager ?? throw new ArgumentNullException(nameof(propertyGridManager));
    }

    // Previous selection properties for state management
    public WmiClassViewModel? PreviousClass { get; private set; }
    public WmiInstanceViewModel? PreviousInstance { get; private set; }
    public WmiNamespaceViewModel? PreviousNamespace { get; private set; }

    public object? PreviousObject { get; private set; }

    /// <summary>
    /// PropertyGridManager for UI binding - exposes PropertyGrid properties
    /// </summary>
    public PropertyGridManager PropertyGrid => _propertyGrid;

    // Convenience properties for PropertyChanged notifications (internal use only)
    public WmiClassViewModel? SelectedClass => SelectedNamespace?.SelectedClass;
    public WmiInstanceViewModel? SelectedInstance => SelectedNamespace?.SelectedClass?.SelectedInstance;

    // Selection state for coordination between ViewModels
    public object? SelectedObject { get; private set; }

    /// <summary>
    /// Clears all selection state, including SelectedNamespace, SelectedClass, SelectedInstance,
    /// and SelectedObject. Also clears the PropertyGrid selection.
    /// </summary>
    public void ClearSelections()
    {
        try
        {
            // Clear selection properties
            SelectedNamespace = null;
            SelectedObject = null;
            PreviousObject = null;

            // Clear PropertyGrid selection
            _propertyGrid.UpdatePropertyGridFromSelection(null);

            // Publish a message to notify other ViewModels
            PublishSelectionChanged();

            // Publish messsage to update the tab count
            _messengerService.Send(new TabCountChangedMessage());
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to clear selections");
        }
    }

    /// <summary>
    /// Gets the currently selected class from the selected namespace.
    /// </summary>
    /// <returns>The selected class or null if no namespace/class selected</returns>
    public WmiClassViewModel? GetSelectedClass() => SelectedNamespace?.SelectedClass;

    /// <summary>
    /// Gets the currently selected instance from the selected class.
    /// </summary>
    /// <returns>The selected instance or null if no namespace/class/instance selected</returns>
    public WmiInstanceViewModel? GetSelectedInstance() => SelectedNamespace?.SelectedClass?.SelectedInstance;

    /// <summary>
    /// Sets the selected object and optionally updates the PropertyGrid.
    /// This method ensures proper operation order: selection operations always happen first,
    /// then PropertyGrid updates if requested.
    /// </summary>
    /// <param name="selectedObject">The object to select</param>
    /// <param name="updatePropertyGrid">Whether to update the PropertyGrid</param>
    public void SetSelectedObject(object? selectedObject, bool updatePropertyGrid = true)
    {
        // CRITICAL: Selection operations must happen BEFORE PropertyGrid updates

        // 1. Manage IsSelected properties first (before any other updates)
        ManageIsSelectedProperties(selectedObject);

        // 2. Update selection state for coordination
        PreviousObject = SelectedObject;
        SelectedObject = selectedObject;

        // 3. Update centralized selection properties
        UpdateCentralizedSelectionProperties(selectedObject);

        // 4. Send SelectionChangedMessage for ViewModels that need it
        PublishSelectionChanged();

        // 5. FINALLY: Update PropertyGrid if requested (always happens last)
        if (updatePropertyGrid)
        {
            _propertyGrid.UpdatePropertyGridFromSelection(selectedObject);
        }
    }

    /// <summary>
    /// Manages IsSelected properties by clearing previous selection and setting new selection.
    /// This ensures local OnIsSelectedChanged actions execute before PropertyGrid updates.
    /// </summary>
    private void ManageIsSelectedProperties(object? selectedObject)
    {
        if (selectedObject == null) return;

        var selectedType = selectedObject.GetType();

        // Clear previous selection of the same type
        if (_previousSelectionsByType.TryGetValue(selectedType, out var previousSelection))
        {
            SetIsSelectedOnViewModel(previousSelection, false);
        }

        // Set new selection
        SetIsSelectedOnViewModel(selectedObject, true);

        // Track this selection for future clearing
        _previousSelectionsByType[selectedType] = selectedObject;
    }

    private void PublishSelectionChanged()
    {
        _messengerService.Send(new SelectionChangedMessage(this));
    }

    /// <summary>
    /// Sets the IsSelected property on any ViewModel that has this property using reflection.
    /// This makes the behavior extensible to any ViewModel without hardcoding specific types.
    /// </summary>
    private static void SetIsSelectedOnViewModel(object? viewModel, bool isSelected)
    {
        if (viewModel == null) return;

        try
        {
            var viewModelType = viewModel.GetType();
            var isSelectedProperty = viewModelType.GetProperty("IsSelected");

            // Only set the property if it exists and is writable
            if (isSelectedProperty != null && isSelectedProperty.CanWrite && isSelectedProperty.PropertyType == typeof(bool))
            {
                isSelectedProperty.SetValue(viewModel, isSelected);
            }
        }
        catch (Exception)
        {
            // Silently ignore reflection errors - not all ViewModels may have IsSelected
            // or may not be accessible, which is fine
        }
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
                {
                    // Raise PropertyChanging event before changing the selection
                    PreviousNamespace = SelectedNamespace;
                    OnPropertyChanging(nameof(SelectedNamespace));
                    SelectedNamespace = namespaceVm;
                }
                break;

            case WmiClassViewModel classVm:
                // Update the namespace's selected class
                if (SelectedNamespace?.SelectedClass != classVm)
                {
                    PreviousClass = GetSelectedClass();
                    OnPropertyChanging(nameof(SelectedClass));
                    SelectedNamespace!.SelectedClass = classVm;
                }
                break;

            case WmiInstanceViewModel instanceVm:
                // Update the class's selected instance
                if (SelectedNamespace?.SelectedClass?.SelectedInstance != instanceVm)
                {
                    PreviousInstance = GetSelectedInstance();
                    OnPropertyChanging(nameof(SelectedInstance));
                    SelectedNamespace!.SelectedClass!.SelectedInstance = instanceVm;
                }
                break;
            default:
                // For non-hierarchy objects, do nothing
                break;
        }
    }
}