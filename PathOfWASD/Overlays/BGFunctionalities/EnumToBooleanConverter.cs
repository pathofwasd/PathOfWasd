using System.Globalization;
using System.Windows.Data;

namespace PathOfWASD.Overlays.BGFunctionalities;

public class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return false;
        string enumValue = value.ToString();
        string targetValue = parameter.ToString();
        return enumValue.Equals(targetValue, StringComparison.InvariantCultureIgnoreCase);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (!(value is bool boolValue) || parameter == null) 
            return Binding.DoNothing;

        if (!boolValue) 
            return Binding.DoNothing;

        return Enum.Parse(targetType, parameter.ToString());
    }
}