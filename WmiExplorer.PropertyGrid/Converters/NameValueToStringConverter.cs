using System.Globalization;
using System.Windows.Data;

namespace WmiExplorer.PropertyGrid.Converters;

/// <summary>
/// Combines Name and Value for clipboard copy in the property grid context menu.
/// </summary>
public class NameValueToStringConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2)
            return string.Empty;
        var name = values[0]?.ToString() ?? string.Empty;
        var value = values[1]?.ToString() ?? string.Empty;
        return string.IsNullOrEmpty(name) ? value : $"{name}: {value}";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}