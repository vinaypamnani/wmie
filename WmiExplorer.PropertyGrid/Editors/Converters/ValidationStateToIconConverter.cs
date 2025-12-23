using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using WmiExplorer.PropertyGrid.Editors.Core;

namespace WmiExplorer.PropertyGrid.Editors.Converters;

public class ValidationIconInfo
{
    public string Glyph { get; set; } = string.Empty;
    public Brush Brush { get; set; } = Brushes.Gray;
}

public class ValidationStateToIconInfoConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ValidationState state)
        {
            switch (state)
            {
                case ValidationState.Error:
                    return new ValidationIconInfo { Glyph = "\uEA39", Brush = Brushes.Red };
                case ValidationState.Modified:
                    return new ValidationIconInfo { Glyph = "\uE946", Brush = Brushes.Green };
                case ValidationState.Normal:
                default:
                    return new ValidationIconInfo { Glyph = string.Empty, Brush = Brushes.Gray };
            }
        }
        return new ValidationIconInfo { Glyph = string.Empty, Brush = Brushes.Gray };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}