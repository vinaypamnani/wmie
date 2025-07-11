using System.Globalization;
using System.Windows.Data;
using WmiExplorer.PropertyGrid.Editors.Core;

namespace WmiExplorer.PropertyGrid.Editors.Converters;

public class ValidationStateToGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ValidationState state)
        {
            switch (state)
            {
                case ValidationState.Error:
                    return "\uEA39"; // StatusError
                case ValidationState.Modified:
                    return "\uE946"; // StatusInfo
                case ValidationState.Normal:
                default:
                    return string.Empty;
            }
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}