using System.ComponentModel;

namespace WmiExplorer.PropertyGrid;

/// <summary>
/// Represents a category node in the property hierarchy.
/// </summary>
public class PropertyCategoryItem : PropertyHierarchyItem
{
    /// <summary>
    /// Creates a new category item.
    /// </summary>
    public PropertyCategoryItem(string categoryName)
    {
        Name = categoryName;
        DisplayName = categoryName;
        Category = categoryName;
        IsCategory = true;
        HasItems = true;

        // Check initial expansion state from the manager
        IsExpanded = CategoryExpansionManager.Instance.IsCategoryExpanded(categoryName);

        // Subscribe to property changes to handle IsExpanded changes
        PropertyChanged += OnPropertyChanged;
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IsExpanded))
        {
            // Update the expansion state in the category manager
            CategoryExpansionManager.Instance.SetCategoryExpanded(Name, IsExpanded);
        }
    }
}