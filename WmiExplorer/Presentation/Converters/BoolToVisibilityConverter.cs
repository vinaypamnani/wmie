using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WmiExplorer.Presentation.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public bool Collapse { get; set; } = true;
    public bool Invert { get; set; } = false;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isVisible = System.Convert.ToBoolean(value);
        if (Invert) isVisible = !isVisible;

        return isVisible
            ? Visibility.Visible
            : (Collapse ? Visibility.Collapsed : Visibility.Hidden);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (value is Visibility visibility && visibility == Visibility.Visible) ^ Invert;
    }
}