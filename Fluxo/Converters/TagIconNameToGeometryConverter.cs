using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Fluxo.Converters;

public class TagIconNameToGeometryConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string iconName || string.IsNullOrWhiteSpace(iconName))
            return null;

        var key = $"Tag.{iconName}";
        return Application.Current?.TryFindResource(key);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}