using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using WmiExplorer.PropertyGrid.Editors.Core;

namespace WmiExplorer.PropertyGrid.Editors.Converters
{
    /// <summary>
    /// Converts ValidationState to border brush
    /// </summary>
    public class ValidationStateToBorderConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ValidationState state)
            {
                return state switch
                {
                    ValidationState.Normal => SystemColors.ControlDarkBrush,
                    ValidationState.Modified => Brushes.Green,
                    ValidationState.Error => Brushes.Red,
                    _ => SystemColors.ControlDarkBrush
                };
            }
            return SystemColors.ControlDarkBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 