using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WmiExplorer.PropertyGrid.Converters;

/// <summary>
/// Converts a boolean value to Visibility.
/// Used by the PropertyGrid to control visibility of UI elements.
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// Gets or sets whether to use Collapsed instead of Hidden.
    /// Default is true (use Collapsed).
    /// </summary>
    public bool Collapse { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to invert the logic.
    /// Default is false (visible when true).
    /// </summary>
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