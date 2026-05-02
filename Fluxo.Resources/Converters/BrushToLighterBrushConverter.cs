using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Fluxo.Resources.Converters;

public class BrushToLighterBrushConverter : IValueConverter
{
    private const double LightnessIncrease = 50d;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (!TryGetColor(value, out var color))
            return Brushes.Transparent;
        var (hue, saturation, lightness) = ToHsl(color);

        lightness = Math.Min(100d, lightness + LightnessIncrease);

        var lighterColor = FromHsl(hue, saturation, lightness, color.A);
        SolidColorBrush lighterBrush = new(lighterColor);

        if (lighterBrush.CanFreeze)
            lighterBrush.Freeze();

        return lighterBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private static bool TryGetColor(object value, out Color color)
    {
        switch (value)
        {
            case SolidColorBrush brush:
                color = brush.Color;
                return true;

            case string hex when !string.IsNullOrWhiteSpace(hex):
                try
                {
                    var converted = ColorConverter.ConvertFromString(hex);
                    if (converted is Color parsedColor)
                    {
                        color = parsedColor;
                        return true;
                    }
                }
                catch (FormatException)
                {
                }
                catch (NotSupportedException)
                {
                }

                break;
        }

        color = Colors.Transparent;
        return false;
    }

    private static (double Hue, double Saturation, double Lightness) ToHsl(Color color)
    {
        var red = color.R / 255d;
        var green = color.G / 255d;
        var blue = color.B / 255d;

        var max = Math.Max(red, Math.Max(green, blue));
        var min = Math.Min(red, Math.Min(green, blue));
        var delta = max - min;

        var lightness = (max + min) / 2d;
        var hue = 0d;
        var saturation = 0d;

        if (delta != 0d)
        {
            saturation = delta / (1d - Math.Abs(2d * lightness - 1d));

            if (max == red)
                hue = 60d * ((green - blue) / delta % 6d);
            else if (max == green)
                hue = 60d * ((blue - red) / delta + 2d);
            else
                hue = 60d * ((red - green) / delta + 4d);
        }

        if (hue < 0d)
            hue += 360d;

        return (hue, saturation * 100d, lightness * 100d);
    }

    private static Color FromHsl(double hue, double saturation, double lightness, byte alpha)
    {
        var normalizedSaturation = saturation / 100d;
        var normalizedLightness = lightness / 100d;

        var chroma = (1d - Math.Abs(2d * normalizedLightness - 1d)) * normalizedSaturation;
        var huePrime = hue / 60d;
        var x = chroma * (1d - Math.Abs(huePrime % 2d - 1d));
        var match = normalizedLightness - chroma / 2d;

        var (red, green, blue) = huePrime switch
        {
            >= 0d and < 1d => (chroma, x, 0d),
            >= 1d and < 2d => (x, chroma, 0d),
            >= 2d and < 3d => (0d, chroma, x),
            >= 3d and < 4d => (0d, x, chroma),
            >= 4d and < 5d => (x, 0d, chroma),
            _ => (chroma, 0d, x)
        };

        return Color.FromArgb(
            alpha,
            ToByte(red + match),
            ToByte(green + match),
            ToByte(blue + match));
    }

    private static byte ToByte(double channel)
    {
        return (byte)Math.Clamp(Math.Round(channel * 255d), 0d, 255d);
    }
}