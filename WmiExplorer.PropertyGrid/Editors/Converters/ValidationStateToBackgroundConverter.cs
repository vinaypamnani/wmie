using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using WmiExplorer.PropertyGrid.Editors.Core;


namespace WmiExplorer.PropertyGrid.Editors.Converters
{
    /// <summary>
    /// Converts ValidationState to background brush
    /// </summary>
    public class ValidationStateToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ValidationState state)
            {
                return state switch
                {
                    ValidationState.Normal => GetPropertyGridBackgroundBrush(),
                    ValidationState.Modified => new SolidColorBrush(Color.FromArgb(30, 0, 255, 0)),
                    ValidationState.Error => new SolidColorBrush(Color.FromArgb(30, 255, 0, 0)),
                    _ => GetPropertyGridBackgroundBrush()
                };
            }
            return GetPropertyGridBackgroundBrush();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private static Brush GetPropertyGridBackgroundBrush()
        {
            try
            {
                // Try to get the PropertyGridBackgroundBrush resource
                if (Application.Current?.TryFindResource("PropertyGridBackgroundBrush") is Brush brush)
                {
                    return brush;
                }
            }
            catch
            {
                // Fall back if resource not found
            }

            // Fallback to system background brush
            return SystemColors.WindowBrush;
        }
    }
} 