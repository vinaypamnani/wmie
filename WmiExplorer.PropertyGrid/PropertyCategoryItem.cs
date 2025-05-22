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
    }

    /// <summary>
    /// Override to toggle category expansion state in the manager.
    /// </summary>
    public override bool IsExpanded
    {
        get => base.IsExpanded;
        set
        {
            // Set the value in the base property
            if (base.IsExpanded != value)
            {
                base.IsExpanded = value;

                // Update the expansion state in the category manager
                CategoryExpansionManager.Instance.SetCategoryExpanded(Name, value);
            }
        }
    }
}