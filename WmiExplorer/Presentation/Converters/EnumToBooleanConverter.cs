using System.Globalization;
using System.Windows.Data;

namespace WmiExplorer.Presentation.Converters
{
    public class EnumToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;
            string? enumValue = value.ToString();
            string? targetValue = parameter.ToString();
            if (enumValue == null || targetValue == null)
                return false;
            return enumValue.Equals(targetValue, StringComparison.InvariantCultureIgnoreCase);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter == null || value == null)
                return Binding.DoNothing;
            bool isChecked = value is bool b && b;
            if (!isChecked)
                return Binding.DoNothing;
            string? targetValue = parameter.ToString();
            if (targetValue == null)
                return Binding.DoNothing;
            return Enum.Parse(targetType, targetValue);
        }
    }
}