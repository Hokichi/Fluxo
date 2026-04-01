using System.Globalization;
using System.Windows.Data;

namespace Fluxo.Converters
{
    public class NumberWithCommasConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value switch
            {
                decimal decimalValue => decimalValue.ToString("#,##0", culture),
                double doubleValue => doubleValue.ToString("#,##0", culture),
                float floatValue => floatValue.ToString("#,##0", culture),
                int intValue => intValue.ToString("#,##0", culture),
                long longValue => longValue.ToString("#,##0", culture),
                short shortValue => shortValue.ToString("#,##0", culture),
                byte byteValue => byteValue.ToString("#,##0", culture),
                _ => string.Empty
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
