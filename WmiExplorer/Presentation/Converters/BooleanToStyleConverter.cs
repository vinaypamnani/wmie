using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WmiExplorer.Presentation.Converters
{
    /// <summary>
    /// Converts a boolean value to a Style resource
    /// </summary>
    public class BooleanToStyleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && parameter is string styleKey)
            {
                var style = Application.Current.Resources[styleKey] as Style;
                return style ?? new Style(); // Return empty style if resource not found
            }

            return new Style(); // Return empty style for invalid input
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 