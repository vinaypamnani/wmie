using System.Globalization;
using System.Windows.Data;
using WmiExplorer.Common.Shared;

namespace WmiExplorer.Presentation.Converters;

/// <summary>
/// Converts an ApplicationState to an appropriate Cursor
/// </summary>
public class ApplicationStateToCursorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ApplicationState applicationState)
        {
            // Check if application is busy or in indeterminate state
            if (applicationState.IsBusy)
            {
                return System.Windows.Input.Cursors.Wait;
            }
        }

        // Default cursor for other states
        return System.Windows.Input.Cursors.Arrow;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}