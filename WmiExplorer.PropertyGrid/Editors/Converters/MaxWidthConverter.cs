using System.Globalization;
using System.Windows.Data;

namespace WmiExplorer.PropertyGrid.Editors.Converters;

/// <summary>
/// Converter to calculate MaxWidth for controls based on available space
/// </summary>
public class MaxWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double actualWidth && actualWidth > 0)
        {
            // Parse the parameter as the width to subtract (column0 width + margins/padding)
            double widthToSubtract = 2.0; // Default fallback
            if (parameter != null && double.TryParse(parameter.ToString(), out double paramValue))
            {
                widthToSubtract = paramValue;
            }

            return Math.Max(100, actualWidth - widthToSubtract); // Minimum 100px
        }
        return 300.0; // Fallback width
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}