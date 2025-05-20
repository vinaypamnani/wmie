using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WmiExplorer.PropertyGrid.Converters
{
    /// <summary>
    /// Converts a double value to a GridLength and back.
    /// Used for binding the help pane height to the grid row definition.
    /// </summary>
    public class DoubleToGridLengthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double doubleValue)
            {
                return new GridLength(doubleValue);
            }
            return new GridLength(50); // Default value
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is GridLength gridLength)
            {
                return gridLength.Value;
            }
            return 50.0; // Default value
        }
    }
}