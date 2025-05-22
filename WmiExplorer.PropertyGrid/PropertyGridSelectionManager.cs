using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WmiExplorer.PropertyGrid;

/// <summary>
/// Manages selection state for property items across the entire property grid.
/// Uses a singleton pattern to ensure only one item is selected at a time.
/// </summary>
public class PropertyGridSelectionManager : INotifyPropertyChanged
{
    /// <summary>
    /// Event that is raised when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    private static readonly Lazy<PropertyGridSelectionManager> _instance = new Lazy<PropertyGridSelectionManager>(() => new PropertyGridSelectionManager());
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
    /// Gets or sets the currently selected property item.
    /// </summary>
    public PropertyHierarchyItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (_selectedItem != value)
            {
                // Clear selection on previous item
                if (_selectedItem != null)
                {
                    _selectedItem.IsSelected = false;
                }

                _selectedItem = value;

                // Set selection on new item
                if (_selectedItem != null)
                {
                    _selectedItem.IsSelected = true;
                }

                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Raises the PropertyChanged event.
    /// </summary>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}