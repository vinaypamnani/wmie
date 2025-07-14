using System.Collections;
using System.Globalization;
using System.Windows.Media;
using WmiExplorer.PropertyGrid.Abstractions;

namespace WmiExplorer.PropertyGrid.Converters;

/// <summary>
/// Default converter for formatting property values as strings and converting them back.
/// </summary>
public class DefaultPropertyValueConverter : IPropertyValueConverter
{
    /// <summary>
    /// Gets the priority of this converter.
    /// </summary>
    public int Priority => 0;

    /// <summary>
    /// Determines if this converter can handle the specified property type.
    /// </summary>
    public bool CanConvert(Type? propertyType)
    {
        // Default converter can handle any type
        return true;
    }

    /// <summary>
    /// Converts a string value back to the property's type.
    /// </summary>

    // Lowest priority (default fallback)

    public object? ConvertFromString(string value, Type propertyType)
    {
        if (propertyType == null)
            return null;

        // Handle null value
        if (value == "<null>" || value == "null")
            return null;

        try
        {
            // Handle common types
            if (propertyType == typeof(string))
                return value;

            if (propertyType == typeof(int) || propertyType == typeof(int?))
                return int.Parse(value);

            if (propertyType == typeof(long) || propertyType == typeof(long?))
                return long.Parse(value);

            if (propertyType == typeof(double) || propertyType == typeof(double?))
                return double.Parse(value, CultureInfo.CurrentCulture);

            if (propertyType == typeof(float) || propertyType == typeof(float?))
                return float.Parse(value, CultureInfo.CurrentCulture);

            if (propertyType == typeof(decimal) || propertyType == typeof(decimal?))
                return decimal.Parse(value, CultureInfo.CurrentCulture);

            if (propertyType == typeof(bool) || propertyType == typeof(bool?))
                return bool.Parse(value);

            if (propertyType == typeof(DateTime) || propertyType == typeof(DateTime?))
                return DateTime.Parse(value, CultureInfo.CurrentCulture);

            if (propertyType == typeof(Guid) || propertyType == typeof(Guid?))
                return Guid.Parse(value);

            if (propertyType.IsEnum)
                return Enum.Parse(propertyType, value);

            // For other types, attempt to use type converter
            var converter = System.ComponentModel.TypeDescriptor.GetConverter(propertyType);
            if (converter != null && converter.CanConvertFrom(typeof(string)))
            {
                return converter.ConvertFromString(value);
            }

            // Last resort: just return the string
            return value;
        }
        catch (Exception)
        {
            // If conversion fails, return the original string value
            return value;
        }
    }

    /// <summary>
    /// Converts a property value to a string for display.
    /// </summary>
    public string ConvertToString(object? value, Type propertyType)
    {
        if (value == null)
            return "<null>";

        // Handle common types with specific formatting
        if (value is DateTime dateTime)
            return dateTime.ToString("G");

        if (value is bool boolValue)
            return boolValue.ToString();

        if (value is Array array)
            return $"{propertyType.Name} (Count: {array.Length})";

        if (value is ICollection collection)
            return $"{propertyType.Name} (Count: {collection.Count})";

        // Default fallback to ToString()
        return value.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Gets a value indicating whether the specified property type should be edited with a custom editor.
    /// </summary>
    public bool RequiresCustomEditor(Type? propertyType)
    {
        // Most basic types don't need a custom editor, they can be edited with a text box
        if (propertyType == null)
            return false;

        // Check for types that would benefit from custom editors
        return propertyType == typeof(bool) ||    // Checkbox
               propertyType.IsEnum ||            // Dropdown
               propertyType == typeof(Color) ||  // Color picker
               propertyType == typeof(DateTime); // Date picker
    }
}