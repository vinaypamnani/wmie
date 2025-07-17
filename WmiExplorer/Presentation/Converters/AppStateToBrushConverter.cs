using System.Globalization;
using System.Windows;
using System.Windows.Data;
using WmiExplorer.Common.Enums;

namespace WmiExplorer.Presentation.Converters;

public class AppStateToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var state = value as AppState? ?? AppState.Indeterminate;
        string brushKey = state switch
        {
            AppState.Indeterminate => "BaseGrayBrush",
            AppState.Error => "BaseRedBrush",
            AppState.Ready => "BaseGreenBrush",
            AppState.Success => "BaseGreenBrush",
            AppState.Warning => "BaseOrangeBrush",
            AppState.Busy => "BaseBlueBrush",
            _ => "BaseGrayBrush"
        };
        return Application.Current.TryFindResource(brushKey) ?? Application.Current.TryFindResource("BaseGrayBrush");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}