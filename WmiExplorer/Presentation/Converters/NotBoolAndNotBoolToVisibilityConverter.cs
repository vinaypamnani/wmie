using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WmiExplorer.Presentation.Converters
{
    public class NotBoolAndNotBoolToVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 2 &&
                values[0] is bool isError &&
                values[1] is bool isBusy)
            {
                return !isError && !isBusy ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}