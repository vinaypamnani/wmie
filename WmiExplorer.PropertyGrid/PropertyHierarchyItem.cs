using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows;
using WmiExplorer.PropertyGrid.Abstractions;

namespace WmiExplorer.PropertyGrid;

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

    private readonly bool _includeNullValues = true;
    private readonly bool _includeReadOnlyProperties = true;
    private readonly bool _includeSystemProperties = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsExpandable))]
    private bool _isExpanded;

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
    }

    /// <summary>
    /// Creates a new instance of PropertyHierarchyItem from a property descriptor.
    /// </summary>
    public PropertyHierarchyItem(IPropertyDescriptor descriptor, int level = 0, bool includeSystemProperties = true, bool includeNullValues = true, bool includeReadOnlyProperties = true)
    {
        PropertyDescriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));        // Initialize from property descriptor
        Name = descriptor.Name;
        DisplayName = descriptor.DisplayName;
        Value = descriptor.Value;
        PropertyType = descriptor.PropertyType ?? typeof(object);
        IsReadOnly = descriptor.IsReadOnly;
        IsKey = descriptor.IsKey;
        Category = descriptor.Category;
        Description = descriptor.Description;
        Level = level;

        _includeSystemProperties = includeSystemProperties;
        _includeNullValues = includeNullValues;
        _includeReadOnlyProperties = includeReadOnlyProperties;

        // Check if this property is expandable
        HasItems = PropertyTypeProviderRegistry.Instance.IsExpandable(Value, PropertyType);

        // Expand by default if the attribute is present
        if (PropertyGridAttributeHelpers.HasPropertyAttribute<ExpandByDefaultAttribute>(descriptor) && IsExpandable)
        {
            IsExpanded = true;
        }
    }

    /// <summary>
    /// Gets child property items.
    /// </summary>
    public ObservableCollection<PropertyHierarchyItem> Children { get; } = new ObservableCollection<PropertyHierarchyItem>();

    /// <summary>
    /// Gets the formatted value as a string.
    /// </summary>
    public string FormattedValue
    {
        get
        {
            var valueType = Value?.GetType() ?? typeof(object);
            var converter = PropertyTypeProviderRegistry.Instance.GetConverter(valueType);
            if (converter == null)
                return Value?.ToString() ?? string.Empty;
            if (Value is Array arr && !(Value is string))
            {
                // Join each element's string representation using the converter
                return string.Join(", ", arr.Cast<object>().Select(v => converter.ConvertToString(v, v?.GetType() ?? typeof(object))));
            }
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
    /// Gets or sets the property descriptor for this item.
    /// </summary>
    public IPropertyDescriptor? PropertyDescriptor { get; set; }

    /// <summary>
    /// Loads child items when the property is expandable.
    /// </summary>
    public virtual void LoadChildren(bool includeSystemProperties = true, bool includeNullValues = true, bool includeReadOnlyProperties = true)
    {
        if (Value == null || !HasItems)
            return;

        try
        {
            var registry = PropertyTypeProviderRegistry.Instance;
            var childDescriptors = registry.GetChildItems(Value, Name, Category).OrderBy(cd => cd.DisplayName).ToList(); if (!includeNullValues)
            {
                childDescriptors = childDescriptors.Where(cd => cd.Value != null).ToList();
            }
            if (!includeReadOnlyProperties)
            {
                childDescriptors = childDescriptors.Where(cd => !cd.IsReadOnly).ToList();
            }
            foreach (var descriptor in childDescriptors)
            {
                if (!includeSystemProperties && descriptor.Name != null && descriptor.Name.StartsWith("__"))
                    continue;
                // If this child has ShowChildrenAsParentAttribute, promote its children
                if (PropertyGridAttributeHelpers.HasPropertyAttribute<ShowChildrenAsParentAttribute>(descriptor))
                {
                    // Use empty string if name/category is null to avoid null reference
                    var grandChildren = registry.GetChildItems(descriptor.Value, descriptor.Name ?? string.Empty, descriptor.Category ?? string.Empty); if (!includeNullValues)
                    {
                        grandChildren = grandChildren.Where(cd => cd.Value != null).ToList();
                    }
                    if (!includeReadOnlyProperties)
                    {
                        grandChildren = grandChildren.Where(cd => !cd.IsReadOnly).ToList();
                    }
                    foreach (var grandChild in grandChildren)
                    {
                        var grandChildItem = new PropertyHierarchyItem(grandChild, Level + 1, includeSystemProperties, includeNullValues, includeReadOnlyProperties);
                        Children.Add(grandChildItem);
                    }
                    continue; // Skip adding this descriptor itself
                }
                var childItem = new PropertyHierarchyItem(descriptor, Level + 1, includeSystemProperties, includeNullValues, includeReadOnlyProperties);
                Children.Add(childItem);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PropertyHierarchyItem] Error loading child properties: {ex.Message}");
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
        // Update the value in the underlying property descriptor if possible
        PropertyDescriptor?.SetValue(Value);
    }

    /// <summary>
    /// Called when IsExpanded property changes
    /// </summary>
    partial void OnIsExpandedChanged(bool value)
    {
        // When expanded, ensure child items are loaded
        if (value && HasItems && Children.Count == 0)
        {
            LoadChildren(_includeSystemProperties, _includeNullValues, _includeReadOnlyProperties);
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