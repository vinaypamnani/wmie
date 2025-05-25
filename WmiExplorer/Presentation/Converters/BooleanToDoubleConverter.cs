using System.Globalization;
using System.Windows.Data;

namespace WmiExplorer.Presentation.Converters;

/// <summary>
/// Converts a boolean value to a double value for dynamic column width sizing.
/// </summary>
public class BooleanToDoubleConverter : IValueConverter
{
    /// <summary>
    /// Gets or sets the value to return when the boolean is false.
    /// Default is 0.0 (hidden).
    /// </summary>
    public double FalseValue { get; set; } = 0.0;

    /// Gets or sets the value to return when the boolean is true.
    /// Default is NaN (Auto sizing).
    /// </summary>
    public double TrueValue { get; set; } = double.NaN;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool boolValue;

        // If we have a parameter, compare the value to the parameter (enum comparison)
        if (parameter != null && value != null)
        {
            string? enumValue = value.ToString();
            string? targetValue = parameter.ToString();
            if (enumValue != null && targetValue != null)
            {
                boolValue = enumValue.Equals(targetValue, StringComparison.InvariantCultureIgnoreCase);
            }
            else
            {
                boolValue = false;
            }
        }
        else
        {
            // Simple boolean conversion
            boolValue = System.Convert.ToBoolean(value);
        }

        return boolValue ? TrueValue : FalseValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double doubleValue)
        {
            return Math.Abs(doubleValue - TrueValue) < Math.Abs(doubleValue - FalseValue);
        }
        return false;
    }
}