namespace WmiExplorer.PropertyGrid.Abstractions;

/// <summary>
/// Represents a generic property descriptor that abstracts property access
/// regardless of the underlying property implementation (reflection, WMI, etc.)
/// </summary>
public interface IPropertyDescriptor
{
    /// <summary>
    /// Gets the category of the property.
    /// </summary>
    string Category { get; }
    /// <summary>
    /// Gets the description of the property.
    /// </summary>
    string Description { get; }
    /// <summary>
    /// Gets the display name of the property.
    /// </summary>
    string DisplayName { get; }
    /// <summary>
    /// Gets whether the property is read-only.
    /// </summary>
    bool IsReadOnly { get; }
    /// <summary>
    /// Gets the name of the property.
    /// </summary>
    string Name { get; }
    /// <summary>
    /// Gets the type of the property.
    /// </summary>
    Type? PropertyType { get; }
    /// <summary>
    /// Gets the source object containing this property.
    /// </summary>
    object Source { get; }
    /// <summary>
    /// Gets the value of the property.
    /// </summary>
    object? Value { get; }

    /// <summary>
    /// Sets the value of the property if it is writable.
    /// </summary>
    /// <param name="value">The value to set</param>
    /// <returns>True if the value was set successfully, false otherwise</returns>
    bool SetValue(object? value);
}