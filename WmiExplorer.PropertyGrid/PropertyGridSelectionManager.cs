using CommunityToolkit.Mvvm.ComponentModel;

namespace WmiExplorer.PropertyGrid;

/// <summary>
/// Manages selection state for property items across the entire property grid.
/// Uses a singleton pattern to ensure only one item is selected at a time.
/// </summary>
public partial class PropertyGridSelectionManager : ObservableObject
{
    private static readonly Lazy<PropertyGridSelectionManager> _instance = new Lazy<PropertyGridSelectionManager>(() => new PropertyGridSelectionManager());

    [ObservableProperty]
    private PropertyHierarchyItem? _selectedItem;

    /// <summary>
    /// Private constructor to prevent external instantiation.
    /// </summary>
    private PropertyGridSelectionManager()
    { }

    /// <summary>
    /// Gets the singleton instance of the selection manager.
    /// </summary>
    public static PropertyGridSelectionManager Instance => _instance.Value;

    /// <summary>
    /// Partial method called when SelectedItem changes.
    /// </summary>
    partial void OnSelectedItemChanged(PropertyHierarchyItem? oldValue, PropertyHierarchyItem? newValue)
    {
        // Clear selection on previous item
        if (oldValue != null)
        {
            oldValue.IsSelected = false;
        }

        // Set selection on new item
        if (newValue != null)
        {
            newValue.IsSelected = true;
        }
    }
}