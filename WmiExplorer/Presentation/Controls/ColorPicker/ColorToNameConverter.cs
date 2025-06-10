using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Windows.Data;
using System.Windows.Media;

namespace WmiExplorer.Presentation.Controls.ColorPicker;

/// <summary>
/// Converts a Color or SolidColorBrush to its known name, or ARGB hex if not known.
/// </summary>
public class ColorToNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        Color color;

        // Support both Color and SolidColorBrush
        if (value is SolidColorBrush brush)
        {
            color = brush.Color;
        }
        else if (value is Color c)
        {
            color = c;
        }
        else
        {
            // Return DependencyProperty.UnsetValue to avoid null reference warnings in WPF binding
            return System.Windows.DependencyProperty.UnsetValue;
        }

        // Try to find a known color name
        var colorProperty = typeof(Colors)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(p => ((Color)p.GetValue(null)!).Equals(color));

        if (colorProperty != null)
        {
            return colorProperty.Name;
        }

        // Fallback to ARGB hex
        return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    [ExcludeFromCodeCoverage]
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}