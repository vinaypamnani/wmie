using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using WmiExplorer.PropertyGrid.Editors.Core;


namespace WmiExplorer.PropertyGrid.Editors.Converters
{
    /// <summary>
    /// Converts ValidationState to border thickness
    /// </summary>
    public class ValidationStateToBorderThicknessConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ValidationState state)
            {
                return state switch
                {
                    ValidationState.Normal => new System.Windows.Thickness(1),
                    ValidationState.Modified => new System.Windows.Thickness(2),
                    ValidationState.Error => new System.Windows.Thickness(2),
                    _ => new System.Windows.Thickness(1)
                };
            }
            return new System.Windows.Thickness(1);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 