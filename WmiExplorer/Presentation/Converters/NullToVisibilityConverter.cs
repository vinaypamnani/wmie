using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WmiExplorer.Presentation.Converters;

/// <summary>
/// Converts null to Collapsed/Hidden, non-null to Visible. Supports Invert and Collapse properties.
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
    public bool Collapse { get; set; } = true;
    public bool Invert { get; set; } = false;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isVisible = value != null;
        if (Invert) isVisible = !isVisible;
        return isVisible
            ? Visibility.Visible
            : (Collapse ? Visibility.Collapsed : Visibility.Hidden);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}