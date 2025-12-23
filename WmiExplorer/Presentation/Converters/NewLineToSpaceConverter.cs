using System.Globalization;
using System.Text;
using System.Windows.Data;

namespace WmiExplorer.Presentation.Converters;

/// <summary>
/// Converts newlines in a string to spaces for single-line display in UI.
/// </summary>
public class NewLineToSpaceConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null)
            return string.Empty;

        var input = value.ToString();
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        // Early return if no newlines are present (most common case)
        if (!input.Contains('\r') && !input.Contains('\n'))
            return input;        // Use StringBuilder for efficient character-by-character replacement
        var result = new StringBuilder(input.Length);
        foreach (char c in input)
        {
            if (c == '\r' || c == '\n')
                result.Append(' ');
            else
                result.Append(c);
        }

        return result.ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Not needed for one-way binding
        return value;
    }
}