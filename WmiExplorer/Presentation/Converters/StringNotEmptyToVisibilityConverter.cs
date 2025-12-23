using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WmiExplorer.Presentation.Converters;

/// <summary>
/// Converts a string to Visibility based on whether it's empty or not.
/// Returns Visible when the string is not empty, otherwise Hidden or Collapsed.
/// </summary>
public class StringNotEmptyToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// Gets or sets whether to use Collapsed instead of Hidden.
    /// Default is true (use Collapsed).
    /// </summary>
    public bool Collapse { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to invert the logic.
    /// Default is false (visible when not empty).
    /// </summary>
    public bool Invert { get; set; } = false;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool hasText = !string.IsNullOrEmpty(value as string);
        if (Invert) hasText = !hasText;

        return hasText
            ? Visibility.Visible
            : (Collapse ? Visibility.Collapsed : Visibility.Hidden);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException("ConvertBack is not implemented for StringNotEmptyToVisibilityConverter");
    }
}