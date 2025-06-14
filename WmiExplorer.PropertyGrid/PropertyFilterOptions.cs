using CommunityToolkit.Mvvm.ComponentModel;

namespace WmiExplorer.PropertyGrid;

/// <summary>
/// Configuration options for filtering properties in the PropertyGrid.
/// Encapsulates the various include/exclude settings in a single, manageable object.
/// </summary>
public partial class PropertyFilterOptions : ObservableObject
{
    [ObservableProperty]
    private bool _includeNullValues = false;

    [ObservableProperty]
    private bool _includeReadOnlyProperties = true;

    [ObservableProperty]
    private bool _includeSystemProperties = true;

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
    /// <param name="includeReadOnlyProperties">Whether to include read-only properties</param>
    /// <param name="includeSystemProperties">Whether to include system properties</param>
    public PropertyFilterOptions(bool includeNullValues, bool includeReadOnlyProperties, bool includeSystemProperties)
    {
        _includeNullValues = includeNullValues;
        _includeReadOnlyProperties = includeReadOnlyProperties;
        _includeSystemProperties = includeSystemProperties;
    }

    /// <summary>
    /// Gets the default filter options for general object properties.
    /// </summary>
    public static PropertyFilterOptions DefaultObjectOptions => new PropertyFilterOptions(
        includeNullValues: true,
        includeReadOnlyProperties: true,
        includeSystemProperties: false);

    /// <summary>
    /// Gets the default filter options for WMI properties.
    /// </summary>
    public static PropertyFilterOptions DefaultWmiOptions => new PropertyFilterOptions(
        includeNullValues: false,
        includeReadOnlyProperties: true,
        includeSystemProperties: true);

    /// <summary>
    /// Creates a deep copy of this PropertyFilterOptions instance.
    /// </summary>
    /// <returns>A new PropertyFilterOptions instance with the same values</returns>
    public PropertyFilterOptions Clone()
    {
        return new PropertyFilterOptions(IncludeNullValues, IncludeReadOnlyProperties, IncludeSystemProperties);
    }

    /// <summary>
    /// Determines whether this instance and another PropertyFilterOptions instance have the same values.
    /// </summary>
    public bool Equals(PropertyFilterOptions? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return IncludeNullValues == other.IncludeNullValues &&
               IncludeReadOnlyProperties == other.IncludeReadOnlyProperties &&
               IncludeSystemProperties == other.IncludeSystemProperties;
    }

    /// <summary>
    /// Returns a hash code for this instance.
    /// </summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(IncludeNullValues, IncludeReadOnlyProperties, IncludeSystemProperties);
    }

    /// <summary>
    /// Determines whether a property should be included based on the current filter options.
    /// </summary>
    /// <param name="propertyName">The name of the property</param>
    /// <param name="propertyValue">The value of the property</param>
    /// <param name="isReadOnly">Whether the property is read-only</param>
    /// <returns>True if the property should be included, false otherwise</returns>
    public bool ShouldIncludeProperty(string? propertyName, object? propertyValue, bool isReadOnly)
    {
        // Filter out null values if not included
        if (!IncludeNullValues && propertyValue == null)
            return false;

        // Filter out read-only properties if not included
        if (!IncludeReadOnlyProperties && isReadOnly)
            return false;

        // Filter out system properties (starting with "__") if not included
        if (!IncludeSystemProperties && propertyName?.StartsWith("__") == true)
            return false;

        return true;
    }
}