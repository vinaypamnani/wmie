using System.ComponentModel;
using System.Reflection;
using WmiExplorer.PropertyGrid.Abstractions;

namespace WmiExplorer.PropertyGrid.Providers;

/// <summary>
/// Property descriptor implementation that uses reflection to access standard .NET properties.
/// </summary>
public class DefaultPropertyDescriptor : IPropertyDescriptor
{
    private readonly string _category;
    private readonly string _description;
    private readonly bool _isReadOnly;
    private readonly PropertyInfo _propertyInfo;
    private readonly object _source;

    /// <summary>
    /// Creates a new DefaultPropertyDescriptor instance.
    /// </summary>
    public DefaultPropertyDescriptor(PropertyInfo propertyInfo, object source, string category = "Misc")
    {
        _propertyInfo = propertyInfo ?? throw new ArgumentNullException(nameof(propertyInfo));
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _category = category;

        // Determine if the property is read-only
        _isReadOnly = !_propertyInfo.CanWrite;

        // Get the display name from attribute or use property name
        var displayNameAttribute = propertyInfo.GetCustomAttribute<DisplayNameAttribute>();
        DisplayName = displayNameAttribute?.DisplayName ?? propertyInfo.Name;

        // Get the description from attribute
        var descriptionAttribute = propertyInfo.GetCustomAttribute<DescriptionAttribute>();
        string attributeDescription = descriptionAttribute?.Description ?? string.Empty;

        // Format the description according to requirements
        _description = FormatPropertyDescription(attributeDescription);
    }

    /// <summary>
    /// Gets the category of the property.
    /// </summary>
    public string Category => _category;

    /// <summary>
    /// Gets the description of the property.
    /// </summary>
    public string Description => _description;

    /// <summary>
    /// Gets the display name of the property.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets whether this property is a key property. Always false for reflection-based properties.
    /// </summary>
    public bool IsKey => false;

    /// <summary>
    /// Gets whether the property is read-only.
    /// </summary>
    public bool IsReadOnly => _isReadOnly;

    /// <summary>
    /// Gets the name of the property.
    /// </summary>
    public string Name => _propertyInfo.Name;

    /// <summary>
    /// Gets the underlying PropertyInfo for this property.
    /// </summary>
    public PropertyInfo PropertyInfo => _propertyInfo;

    /// <summary>
    /// Gets the type of the property.
    /// </summary>
    public Type PropertyType => _propertyInfo.PropertyType;

    /// <summary>
    /// Gets the source object containing this property.
    /// </summary>
    public object Source => _source;

    /// <summary>
    /// Gets the value of the property.
    /// </summary>
    public object? Value
    {
        get
        {
            try
            {
                var rawValue = _propertyInfo.GetValue(_source);
                // Only apply enhanced formatting for integer types
                var enhancedValue = GetEnhancedValue(rawValue);
                return enhancedValue ?? rawValue;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Sets the value of the property if it is writable.
    /// </summary>
    public bool SetValue(object? value)
    {
        if (IsReadOnly)
            return false;

        try
        {
            _propertyInfo.SetValue(_source, value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Helper to format integer values as hex.
    /// </summary>
    /// <param name="value">The value to format</param>
    /// <param name="propertyType">The property type</param>
    /// <returns>Hex formatted string or empty string if not applicable</returns>
    private static string FormatIntegerAsHex(object value, Type? propertyType)
    {
        if (value == null) return string.Empty;

        try
        {
            return propertyType switch
            {
                var t when t == typeof(byte) || t == typeof(byte?) =>
                    $"0x{Convert.ToByte(value):X2}",
                var t when t == typeof(sbyte) || t == typeof(sbyte?) =>
                    $"0x{(byte)Convert.ToSByte(value):X2}",
                var t when t == typeof(ushort) || t == typeof(ushort?) =>
                    $"0x{Convert.ToUInt16(value):X4}",
                var t when t == typeof(short) || t == typeof(short?) =>
                    $"0x{(ushort)Convert.ToInt16(value):X4}",
                var t when t == typeof(uint) || t == typeof(uint?) =>
                    $"0x{Convert.ToUInt32(value):X8}",
                var t when t == typeof(int) || t == typeof(int?) =>
                    $"0x{(uint)Convert.ToInt32(value):X8}",
                var t when t == typeof(ulong) || t == typeof(ulong?) =>
                    $"0x{Convert.ToUInt64(value):X16}",
                var t when t == typeof(long) || t == typeof(long?) =>
                    $"0x{(ulong)Convert.ToInt64(value):X16}",
                var t when t == typeof(char) || t == typeof(char?) =>
                    $"0x{Convert.ToUInt16(value):X4}", // Char as UTF-16 hex
                _ => string.Empty
            };
        }
        catch
        {
            // Return empty string instead of forcing ToString() to avoid potential issues
            return string.Empty;
        }
    }

    /// <summary>
    /// Formats the property description according to the standard format.
    /// </summary>
    /// <param name="attributeDescription">The original description from attribute if any</param>
    /// <returns>Formatted description</returns>
    private string FormatPropertyDescription(string attributeDescription)
    {
        // Format the description according to requirements for non-WMI types
        string arrayIndicator = PropertyType.IsArray ? " (Array)" : "";
        string typeDescription = $"Type: {PropertyType.Name}{arrayIndicator}";

        // If we have an attribute description, include it after the type information
        if (!string.IsNullOrEmpty(attributeDescription))
        {
            return $"{typeDescription}\n{attributeDescription}";
        }
        else
        {
            return typeDescription;
        }
    }

    /// <summary>
    /// Enhanced value display with hex formatting for integer types.
    /// </summary>
    /// <param name="rawValue">The raw value to enhance</param>
    /// <returns>Enhanced value with hex formatting if applicable</returns>
    private object? GetEnhancedValue(object? rawValue)
    {
        if (rawValue == null)
            return null;

        // Add hex formatting for integer types
        var hex = FormatIntegerAsHex(rawValue, PropertyType);
        if (!string.IsNullOrEmpty(hex))
        {
            return $"{rawValue} [{hex}]";
        }

        return rawValue;
    }
}