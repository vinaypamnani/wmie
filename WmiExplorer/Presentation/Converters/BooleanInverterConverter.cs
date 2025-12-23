using System.Globalization;
using System.Windows.Data;

namespace WmiExplorer.Presentation.Converters;

/// <summary>
/// Converts a boolean value to its inverse (true becomes false, false becomes true).
/// Useful for properties like IsReadOnly where you want the opposite of a boolean value.
/// </summary>
public class BooleanInverterConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }

        // If the value is not a boolean, try to convert it
        try
        {
            bool convertedValue = System.Convert.ToBoolean(value);
            return !convertedValue;
        }
        catch
        {
            // If conversion fails, return false as default
            return false;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }

        // If the value is not a boolean, try to convert it
        try
        {
            bool convertedValue = System.Convert.ToBoolean(value);
            return !convertedValue;
        }
        catch
        {
            // If conversion fails, return true as default
            return true;
        }
    }
}