using System.Globalization;
using System.Windows.Data;
using WmiExplorer.Models;

namespace WmiExplorer.Presentation.Converters;

/// <summary>
/// Safely gets a property value from a WmiInstance by property name and returns it as a string for display.
/// </summary>
public class SafePropertyValueConverter : IMultiValueConverter
{
    public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2)
            return string.Empty;
        if (values[0] is WmiInstance instance && values[1] is string propertyName)
        {
            var value = instance.GetPropertyValue(propertyName);
            return value?.ToString() ?? string.Empty;
        }
        return string.Empty;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}