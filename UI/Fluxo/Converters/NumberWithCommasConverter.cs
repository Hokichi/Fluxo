using System.Globalization;
using System.Windows.Data;

namespace Fluxo.Converters;

public class NumberWithCommasConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return MoneyFormatUtility.ToCompactText(value, culture);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
