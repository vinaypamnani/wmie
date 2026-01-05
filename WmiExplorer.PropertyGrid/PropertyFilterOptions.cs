using CommunityToolkit.Mvvm.ComponentModel;

namespace WmiExplorer.PropertyGrid;

/// <summary>
/// Configuration options for filtering properties in the PropertyGrid.
/// Encapsulates the various include/exclude settings in a single, manageable object.
/// </summary>
public partial class PropertyFilterOptions : ObservableObject
{
    /// <summary>
    /// Whether to allow editing read-only properties. When enabled, read-only properties can be edited,
    /// but WMI providers may or may not support editing these properties.
    /// </summary>
    [ObservableProperty]
    private bool _allowEditingReadOnlyProperties = false;

    [ObservableProperty]
    private bool _includeNullValues = false;

    [ObservableProperty]
    private bool _includeSystemProperties = true;

    /// <summary>
    /// Whether the PropertyGrid is in read-only mode. This affects how read-only properties are filtered.
    /// </summary>
    private bool _isPropertyGridReadOnly = true;

    /// <summary>
    /// Creates a new instance with default filter options.
    /// </summary>
    public PropertyFilterOptions()
    {
    }

    /// <summary>
    /// Creates a new instance with specified filter options.
    /// </summary>
    /// <param name="includeNullValues">Whether to include properties with null values</param>
    /// <param name="includeSystemProperties">Whether to include system properties</param>
    /// <param name="allowEditingReadOnlyProperties">Whether to allow editing read-only properties (also controls visibility in writable mode)</param>
    /// <param name="isPropertyGridReadOnly">Whether the PropertyGrid is in read-only mode (defaults to true)</param>
    public PropertyFilterOptions(bool includeNullValues, bool includeSystemProperties, bool allowEditingReadOnlyProperties = false, bool isPropertyGridReadOnly = true)
    {
        _includeNullValues = includeNullValues;
        _includeSystemProperties = includeSystemProperties;
        _allowEditingReadOnlyProperties = allowEditingReadOnlyProperties;
        _isPropertyGridReadOnly = isPropertyGridReadOnly;
    }

    /// <summary>
    /// Gets the default filter options for general object properties.
    /// Note: PropertyGrid uses DefaultWmiOptions by default. This option is available for non-WMI scenarios.
    /// </summary>
    public static PropertyFilterOptions DefaultObjectOptions => new PropertyFilterOptions(
        includeNullValues: true,
        includeSystemProperties: false);

    /// <summary>
    /// Gets the default filter options for WMI properties.
    /// This is the standard default used by PropertyGrid.
    /// </summary>
    public static PropertyFilterOptions DefaultWmiOptions => new PropertyFilterOptions(
        includeNullValues: false,
        includeSystemProperties: true);

    /// <summary>
    /// Gets or sets whether the PropertyGrid is in read-only mode.
    /// </summary>
    public bool IsPropertyGridReadOnly
    {
        get => _isPropertyGridReadOnly;
        set => SetProperty(ref _isPropertyGridReadOnly, value);
    }

    /// <summary>
    /// Creates a deep copy of this PropertyFilterOptions instance.
    /// </summary>
    /// <returns>A new PropertyFilterOptions instance with the same values</returns>
    public PropertyFilterOptions Clone()
    {
        return new PropertyFilterOptions(IncludeNullValues, IncludeSystemProperties, AllowEditingReadOnlyProperties, IsPropertyGridReadOnly);
    }

    /// <summary>
    /// Determines whether this instance and another PropertyFilterOptions instance have the same values.
    /// </summary>
    public bool Equals(PropertyFilterOptions? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return IncludeNullValues == other.IncludeNullValues &&
               IncludeSystemProperties == other.IncludeSystemProperties &&
               AllowEditingReadOnlyProperties == other.AllowEditingReadOnlyProperties &&
               IsPropertyGridReadOnly == other.IsPropertyGridReadOnly;
    }

    /// <summary>
    /// Returns a hash code for this instance.
    /// </summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(IncludeNullValues, IncludeSystemProperties, AllowEditingReadOnlyProperties, IsPropertyGridReadOnly);
    }

    /// <summary>
    /// Determines whether a property should be included based on the current filter options.
    /// </summary>
    /// <param name="propertyName">The name of the property</param>
    /// <param name="propertyValue">The value of the property</param>
    /// <param name="isReadOnly">Whether the property is read-only</param>
    /// <param name="isKey">Whether the property is a key property (optional, defaults to false)</param>
    /// <param name="isPropertyGridReadOnly">Whether the PropertyGrid is read-only (optional, defaults to the stored IsPropertyGridReadOnly value)</param>
    /// <returns>True if the property should be included, false otherwise</returns>
    public bool ShouldIncludeProperty(string? propertyName, object? propertyValue, bool isReadOnly, bool isKey = false, bool? isPropertyGridReadOnly = null)
    {
        // Filter out null values if not included
        if (!IncludeNullValues && propertyValue == null)
            return false;

        // Always include key properties, even if read-only properties are filtered out
        if (isKey)
            return true;

        // Use provided parameter or fall back to stored value (defaults to true for read-only mode)
        bool currentIsPropertyGridReadOnly = isPropertyGridReadOnly ?? IsPropertyGridReadOnly;

        // In writable mode (currentIsPropertyGridReadOnly = false): use AllowEditingReadOnlyProperties to control visibility
        // In read-only mode (currentIsPropertyGridReadOnly = true): always show read-only properties
        if (!currentIsPropertyGridReadOnly && !AllowEditingReadOnlyProperties && isReadOnly)
            return false; // Hide in writable mode when editing is disabled

        // Filter out system properties (starting with "__") if not included
        if (!IncludeSystemProperties && propertyName?.StartsWith("__") == true)
            return false;

        return true;
    }
}