using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WmiExplorer.Presentation.Converters;

/// <summary>
/// Returns 0 if the MenuItem is a top-level item and has no icon, otherwise returns Auto.
/// </summary>
public class TopLevelMenuIconColumnWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        // values[0]: Role (MenuItemRole)
        // values[1]: Icon Content
        if (values.Length < 2)
            return new GridLength(24);

        var role = values[0]?.ToString();
        var iconContent = values[1];

        if (role == "TopLevelItem" && iconContent == null)
            return new GridLength(0);

        return GridLength.Auto;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}