using System.Diagnostics;
using System.Windows.Data;

namespace WmiExplorer.Presentation.Converters;

public class DebugConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        Debugger.Break();
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        Debugger.Break();
        return value;
    }
}