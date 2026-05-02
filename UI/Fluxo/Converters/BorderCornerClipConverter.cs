using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Fluxo.Converters;

public class BorderCornerClipConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values[0] is not CornerRadius cornerRadius ||
            !double.TryParse(values[1].ToString(), out var width) ||
            !double.TryParse(values[2].ToString(), out var height))
            throw new Exception("Invalid Parameters");

        return new RectangleGeometry
        {
            RadiusX = cornerRadius.TopLeft,
            RadiusY = cornerRadius.BottomRight,
            Rect = new Rect(0, 0, width, height)
        };
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}