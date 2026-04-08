using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Fluxo.Converters;

public class BrushToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not SolidColorBrush brush)
            return Colors.Transparent;

        return brush.Color;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}