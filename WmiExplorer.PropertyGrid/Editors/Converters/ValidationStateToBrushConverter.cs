using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using WmiExplorer.PropertyGrid.Editors.Core;

namespace WmiExplorer.PropertyGrid.Editors.Converters;

public class ValidationStateToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ValidationState state)
        {
            switch (state)
            {
                case ValidationState.Error:
                    return Brushes.Red;
                case ValidationState.Modified:
                    return Brushes.Green;
                case ValidationState.Normal:
                default:
                    // Try to use PropertyGridForegroundBrush
                    try
                    {
                        if (Application.Current.TryFindResource("PropertyGridForegroundBrush") is Brush brush)
                            return brush;
                    }
                    catch { }
                    return Brushes.Gray;
            }
        }
        return Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}