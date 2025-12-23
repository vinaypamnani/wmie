using System.Globalization;
using System.Windows.Data;
using Binding = System.Windows.Data.Binding;

namespace WmiExplorer.Presentation.Converters;

/// <summary>
/// Converter that handles converting between enum flag values and boolean checkbox values
/// </summary>
public class EnumFlagsToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false;

        try
        {
            // Convert from enum value to boolean
            var enumValue = (Enum)value;
            var paramStr = parameter.ToString() ?? string.Empty;
            var flag = Enum.Parse(enumValue.GetType(), paramStr);

            return enumValue.HasFlag((Enum)flag);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null || !(value is bool boolValue))
            return Binding.DoNothing;

        if (!targetType.IsEnum)
            return Binding.DoNothing;

        try
        {
            // Use ConverterParameter to determine which flag to toggle
            var parameterStr = parameter.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(parameterStr))
                return Binding.DoNothing;

            // Get the flag enum value from the parameter string
            var enumType = targetType;
            if (!Enum.TryParse(enumType, parameterStr, out object? flagValue) || flagValue == null)
                return Binding.DoNothing;

            // Return a custom value that the WPF binding system can use to update the source
            // When the checkbox is checked, we want to set the flag, otherwise clear it
            if (boolValue)
            {
                // Return the flag value that needs to be set (ORed with current value)
                return flagValue;
            }
            else
            {
                // Return a special value indicating the flag should be cleared
                // The negative value will be used as a signal to clear the flag
                int intValue = System.Convert.ToInt32(flagValue);
                return Enum.ToObject(enumType, ~intValue);
            }
        }
        catch (Exception)
        {
            return Binding.DoNothing;
        }
    }
}