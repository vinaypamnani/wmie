using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using WmiExplorer.PropertyGrid.Editors.Core;

namespace WmiExplorer.PropertyGrid.Editors.Converters;

public class ValidationStateToIconConverter : IValueConverter
{
    // Returns a tuple: (glyph, brush)
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ValidationState state)
        {
            switch (state)
            {
                case ValidationState.Error:
                    // Error icon (Segoe Fluent: ErrorCircle, U+EA39)
                    return ("\uEA39", Brushes.Red);
                case ValidationState.Modified:
                    // Success/modified icon (Segoe Fluent: CheckmarkCircle, U+E73E)
                    return ("\uE73E", Brushes.Green);
                case ValidationState.Normal:
                default:
                    return null; // No icon
            }
        }
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}