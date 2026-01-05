using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows;
using WmiExplorer.PropertyGrid.Abstractions;

namespace WmiExplorer.PropertyGrid;

/// <summary>
/// Custom comparer for array indices that handles numeric sorting properly.
/// Sorts array indices like [0], [1], [2], ..., [10], [11] in the correct numerical order
/// instead of alphabetical order where [10] would come before [2].
/// </summary>
public class ArrayIndexComparer : IComparer<string>
{
    private static readonly Regex ArrayIndexPattern = new(@"^\[(\d+)\]$", RegexOptions.Compiled);

    public int Compare(string? x, string? y)
    {
        // Handle null cases
        if (x == null && y == null) return 0;
        if (x == null) return -1;
        if (y == null) return 1;

        // Check if both strings are array indices
        var matchX = ArrayIndexPattern.Match(x);
        var matchY = ArrayIndexPattern.Match(y);

        // If both are array indices, compare numerically
        if (matchX.Success && matchY.Success)
        {
            if (int.TryParse(matchX.Groups[1].Value, out int indexX) &&
                int.TryParse(matchY.Groups[1].Value, out int indexY))
            {
                return indexX.CompareTo(indexY);
            }
        }

        // If only one is an array index, array indices come first
        if (matchX.Success && !matchY.Success) return -1;
        if (!matchX.Success && matchY.Success) return 1;

        // For non-array indices, use regular string comparison
        return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Represents a property item in the hierarchical property grid.
/// This is the base class for all items in the tree structure.
/// </summary>
public partial class PropertyHierarchyItem : ObservableObject
{
    [ObservableProperty]
    private string _category = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    private readonly PropertyFilterOptions _filterOptions;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsExpandable))]
    private bool _isExpanded;

    private bool _isInitializing = true;

    [ObservableProperty]
    private bool _isKey;

    [ObservableProperty]
    private bool _isReadOnly;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private Type _propertyType = typeof(object);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedValue))]
    private object? _value;

    [ObservableProperty]
    private Visibility _visibility = Visibility.Visible;

    /// <summary>
    /// Creates a new instance of PropertyHierarchyItem.
    /// </summary>
    public PropertyHierarchyItem()
    {
        _filterOptions = PropertyFilterOptions.DefaultWmiOptions;
        OriginalValue = Value;
        _isInitializing = false; // Initialization complete
    }

    /// <summary>
    /// Creates a new instance of PropertyHierarchyItem from a property descriptor.
    /// </summary>
    public PropertyHierarchyItem(IPropertyDescriptor descriptor, int level = 0, PropertyFilterOptions? filterOptions = null)
    {
        PropertyDescriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));

        // Use provided filter options or create default (use WMI defaults for consistency with PropertyGrid)
        filterOptions ??= PropertyFilterOptions.DefaultWmiOptions;
        _filterOptions = filterOptions;

        // Initialize from property descriptor
        Name = descriptor.Name;
        DisplayName = descriptor.DisplayName;
        Value = descriptor.Value;
        OriginalValue = Value;
        PropertyType = descriptor.PropertyType ?? typeof(object);
        IsReadOnly = descriptor.IsReadOnly;
        IsKey = descriptor.IsKey;
        Category = descriptor.Category;
        Description = descriptor.Description; Level = level;

        // Check if this property is expandable
        HasItems = PropertyTypeProviderRegistry.Instance.IsExpandable(Value, PropertyType);

        // Expand by default if the attribute is present
        if (PropertyGridAttributeHelpers.HasPropertyAttribute<ExpandByDefaultAttribute>(descriptor) && IsExpandable)
        {
            IsExpanded = true;
        }

        _isInitializing = false; // Initialization complete
    }

    /// <summary>
    /// Gets child property items.
    /// </summary>
    public ObservableCollection<PropertyHierarchyItem> Children { get; } = new ObservableCollection<PropertyHierarchyItem>();

    /// <summary>
    /// Gets the filter options associated with this property hierarchy item.
    /// </summary>
    public PropertyFilterOptions FilterOptions => _filterOptions;

    /// <summary>
    /// Gets the formatted value as a string.
    /// </summary>
    public string FormattedValue
    {
        get
        {
            if (Value == DependencyProperty.UnsetValue)
                return string.Empty;

            var valueType = Value?.GetType() ?? typeof(object);
            var converter = PropertyTypeProviderRegistry.Instance.GetConverter(valueType);
            if (converter == null)
                return Value?.ToString() ?? string.Empty;
            // For arrays, use the converter's array logic, not string.Join
            return converter.ConvertToString(Value, valueType);
        }
    }

    /// <summary>
    /// Gets or sets whether this item has child items.
    /// </summary>
    public bool HasItems { get; set; }

    /// <summary>
    /// Gets or sets whether this item represents a category.
    /// </summary>
    public bool IsCategory { get; set; }

    /// <summary>
    /// Gets whether this item is expandable in the TreeView.
    /// This is used by the TreeView to determine whether to show the expander arrow.
    /// </summary>
    public bool IsExpandable
    {
        get
        {
            // Always expandable if it already has children loaded
            if (Children.Count > 0)
                return true;

            // Otherwise check if it has items that can be loaded
            return HasItems;
        }
    }

    /// <summary>
    /// Gets or sets the level in the hierarchy (used for indentation).
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// Stores the original value for validation. Set only once at construction.
    /// </summary>
    public object? OriginalValue { get; }

    /// <summary>
    /// Gets or sets the property descriptor for this item.
    /// </summary>
    public IPropertyDescriptor? PropertyDescriptor { get; set; }

    /// <summary>
    /// Loads child items when the property is expandable.
    /// </summary>
    public virtual void LoadChildren(PropertyFilterOptions? filterOptions = null)
    {
        if (Value == null || !HasItems)
            return;

        // Use provided filter options or current filter options
        filterOptions ??= _filterOptions;
        try
        {
            var registry = PropertyTypeProviderRegistry.Instance;
            var childDescriptors = registry.GetChildItems(Value, Name, Category).ToList();

            // Sort by Value for NameValueCollection, otherwise sort by DisplayName
            if (Value is System.Collections.Specialized.NameValueCollection)
            {
                // Check if all values can be parsed as integers for numeric sorting
                var allIntValues = childDescriptors.All(cd =>
                    cd.Value != null && int.TryParse(cd.Value.ToString(), out _));

                if (allIntValues)
                {
                    childDescriptors = childDescriptors.OrderBy(cd =>
                        int.TryParse(cd.Value?.ToString(), out int intValue) ? intValue : int.MaxValue).ToList();
                }
                else
                {
                    childDescriptors = childDescriptors.OrderBy(cd => cd.Value?.ToString() ?? string.Empty, StringComparer.OrdinalIgnoreCase).ToList();
                }
            }
            else
            {
                childDescriptors = childDescriptors.OrderBy(cd => cd.DisplayName, new ArrayIndexComparer()).ToList();
            }

            // Filter child descriptors based on filter options
            // Use the IsPropertyGridReadOnly value stored in filterOptions
            childDescriptors = childDescriptors.Where(cd => filterOptions.ShouldIncludeProperty(cd.Name, cd.Value, cd.IsReadOnly, cd.IsKey, filterOptions.IsPropertyGridReadOnly)).ToList();

            foreach (var descriptor in childDescriptors)
            {
                // If this child has ShowChildrenAsParentAttribute, promote its children
                if (PropertyGridAttributeHelpers.HasPropertyAttribute<ShowChildrenAsParentAttribute>(descriptor))
                {
                    // Use empty string if name/category is null to avoid null reference
                    var grandChildren = registry.GetChildItems(descriptor.Value, descriptor.Name ?? string.Empty, descriptor.Category ?? string.Empty);

                    // Filter grandchildren based on filter options
                    // Use the IsPropertyGridReadOnly value stored in filterOptions
                    grandChildren = grandChildren.Where(gc => filterOptions.ShouldIncludeProperty(gc.Name, gc.Value, gc.IsReadOnly, gc.IsKey, filterOptions.IsPropertyGridReadOnly)).ToList();

                    foreach (var grandChild in grandChildren)
                    {
                        var grandChildItem = new PropertyHierarchyItem(grandChild, Level + 1, filterOptions);
                        Children.Add(grandChildItem);
                    }
                    continue; // Skip adding this descriptor itself
                }
                var childItem = new PropertyHierarchyItem(descriptor, Level + 1, filterOptions);
                Children.Add(childItem);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PropertyHierarchyItem] Error loading child properties: {ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// Resets visibility for this item and all its children recursively.
    /// Used when clearing a search filter.
    /// </summary>
    public virtual void ResetVisibilityRecursive()
    {
        // Reset this item's visibility
        Visibility = Visibility.Visible;

        // Reset all children's visibility recursively
        foreach (var child in Children)
        {
            child.ResetVisibilityRecursive();
        }
    }

    /// <summary>
    /// Called when the property value changes.
    /// </summary>
    protected virtual void OnPropertyValueChanged()
    {
        // Only update the underlying property descriptor for user-initiated changes,
        // not during initialization or for read-only properties
        if (PropertyDescriptor != null && !IsReadOnly && !_isInitializing)
        {
            PropertyDescriptor.SetValue(Value);
        }
    }

    /// <summary>
    /// Called when IsExpanded property changes
    /// </summary>
    partial void OnIsExpandedChanged(bool value)
    {
        // When expanded, ensure child items are loaded
        if (value && HasItems && Children.Count == 0)
        {
            LoadChildren(_filterOptions);
        }
    }

    /// <summary>
    /// Called when IsSelected property changes
    /// </summary>
    partial void OnIsSelectedChanged(bool value)
    {
        // When this item is selected, notify the grid
        if (value)
        {
            PropertyGridSelectionManager.Instance.SelectedItem = this;
        }
    }

    /// <summary>
    /// Called when Value property changes
    /// </summary>
    partial void OnValueChanged(object? value)
    {
        OnPropertyValueChanged();
    }
}