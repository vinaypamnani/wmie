using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WmiExplorer.Presentation.Converters;

/// <summary>
/// Converts multiple boolean values to Visibility.Visible if all are true, otherwise Collapsed.
/// </summary>
public class MultiBooleanAndToVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length == 0)
            return Visibility.Collapsed;

        foreach (var value in values)
        {
            if (value is bool b)
            {
                if (!b)
                    return Visibility.Collapsed;
            }
            else
            {
                return Visibility.Collapsed;
            }
        }
        return Visibility.Visible;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}