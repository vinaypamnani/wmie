using System;
using System.Globalization;
using System.Windows.Data;

namespace WmiExplorer.PropertyGrid.Converters
{
    /// <summary>
    /// Converts newlines in a string to spaces for single-line display in the property grid.
    /// </summary>
    public class NewlineToSpaceConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length == 0 || values[0] == null)
                return string.Empty;
            var input = values[0].ToString();
            if (string.IsNullOrEmpty(input))
                return string.Empty;
            // Replace all newline characters with a single space
            return input.Replace("\r", " ").Replace("\n", " ");
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
