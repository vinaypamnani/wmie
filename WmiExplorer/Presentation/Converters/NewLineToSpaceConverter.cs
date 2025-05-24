using System.Globalization;
using System.Windows.Data;

namespace WmiExplorer.Presentation.Converters;

/// <summary>
/// Converts newlines in a string to spaces for single-line display in UI.
/// </summary>
public class NewLineToSpaceConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string s)
            return s.Replace("\r", " ").Replace("\n", " ");
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Not needed for one-way binding
        return value;
    }
}