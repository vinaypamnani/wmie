using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WmiExplorer.Presentation.Converters;

/// <summary>
/// Converts an enum value to Visibility by comparing it to a parameter.
/// Returns Visible when the enum equals the parameter, otherwise Collapsed/Hidden.
/// </summary>
public class EnumToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// Gets or sets whether to use Collapsed instead of Hidden.
    /// Default is true (use Collapsed).
    /// </summary>
    public bool Collapse { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to invert the logic.
    /// Default is false (visible when enum matches parameter).
    /// </summary>
    public bool Invert { get; set; } = false;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isVisible = false;

        if (value != null && parameter != null)
        {
            string? enumValue = value.ToString();
            string? targetValue = parameter.ToString();
            if (enumValue != null && targetValue != null)
            {
                isVisible = enumValue.Equals(targetValue, StringComparison.InvariantCultureIgnoreCase);
            }
        }

        if (Invert)
            isVisible = !isVisible;

        return isVisible
            ? Visibility.Visible
            : (Collapse ? Visibility.Collapsed : Visibility.Hidden);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
