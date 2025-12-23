using CommunityToolkit.Mvvm.ComponentModel;

namespace WmiExplorer.PropertyGrid;

/// <summary>
/// Manages category expansion state for the property grid.
/// </summary>
public class CategoryExpansionManager : ObservableObject
{
    private static readonly Dictionary<string, bool> _expandedCategories = new Dictionary<string, bool>();
    private static CategoryExpansionManager? _instance;

    // Private constructor for singleton pattern
    private CategoryExpansionManager()
    { }

    /// <summary>
    /// Gets the singleton instance of the category expansion manager.
    /// </summary>
    public static CategoryExpansionManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new CategoryExpansionManager();
            }
            return _instance;
        }
    }

    /// <summary>
    /// Gets whether a category is expanded.
    /// </summary>
    public bool IsCategoryExpanded(string category)
    {
        if (string.IsNullOrEmpty(category))
            return true;

        if (!_expandedCategories.ContainsKey(category))
        {
            // Default to expanded for new categories
            _expandedCategories[category] = true;
        }

        return _expandedCategories[category];
    }

    /// <summary>
    /// Sets the expanded state of a category.
    /// </summary>
    public void SetCategoryExpanded(string category, bool expanded)
    {
        if (string.IsNullOrEmpty(category))
            return;

        if (!_expandedCategories.ContainsKey(category) || _expandedCategories[category] != expanded)
        {
            _expandedCategories[category] = expanded;
            OnPropertyChanged(category);
        }
    }

    /// <summary>
    /// Toggles the expanded state of a category.
    /// </summary>
    public void ToggleCategory(string category)
    {
        if (string.IsNullOrEmpty(category))
            return;

        bool currentState = IsCategoryExpanded(category);
        SetCategoryExpanded(category, !currentState);
    }
}