using System.Globalization;
using System.Windows.Data;

namespace WmiExplorer.Presentation.Converters;

// Converts null or empty strings to null, otherwise returns the string itself
public class StringNullOrEmptyToNullConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var str = value as string;
        return string.IsNullOrEmpty(str) ? null : str;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}