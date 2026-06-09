using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Fluxo.Resources.Converters;

public sealed class ForegroundForBackgroundBrushConverter : IValueConverter, IMultiValueConverter
{
    private const double BrightBackgroundLuminanceThreshold = 0.5d;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var currentForeground = ResolveBrush(parameter, "Brush.Text.Primary");
        return ConvertForeground(value, currentForeground);
    }

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var background = values.Length > 0 ? values[0] : null;
        var foreground = values.Length > 1 ? values[1] : parameter;
        var currentForeground = ResolveBrush(foreground, "Brush.Text.Primary");

        return ConvertForeground(background, currentForeground);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private static Brush ConvertForeground(object? background, Brush currentForeground)
    {
        if (!TryGetColor(background, out var backgroundColor))
            return currentForeground;

        if (GetRelativeLuminance(backgroundColor) < BrightBackgroundLuminanceThreshold)
            return currentForeground;

        if (TryFindDarkTextBrush(currentForeground, out var darkForeground))
            return darkForeground;

        return ResolveBrush("Brush.Text.Primary.Dark", currentForeground);
    }

    private static bool TryFindDarkTextBrush(Brush foreground, out Brush darkForeground)
    {
        if (ReferenceEquals(foreground, TryFindResource("Brush.Text.Primary")))
            return TryResolveBrush("Brush.Text.Primary.Dark", out darkForeground);

        if (ReferenceEquals(foreground, TryFindResource("Brush.Text.Secondary")))
            return TryResolveBrush("Brush.Text.Secondary.Dark", out darkForeground);

        if (ReferenceEquals(foreground, TryFindResource("Brush.Text.Muted")))
            return TryResolveBrush("Brush.Text.Muted.Dark", out darkForeground);

        darkForeground = foreground;
        return false;
    }

    private static Brush ResolveBrush(object? value, string fallbackResourceKey)
    {
        return value switch
        {
            Brush brush => brush,
            string resourceKey when TryResolveBrush(resourceKey, out var brush) => brush,
            _ when TryResolveBrush(fallbackResourceKey, out var fallbackBrush) => fallbackBrush,
            _ => Brushes.White
        };
    }

    private static Brush ResolveBrush(string resourceKey, Brush fallbackBrush)
    {
        return TryResolveBrush(resourceKey, out var brush) ? brush : fallbackBrush;
    }

    private static bool TryResolveBrush(string resourceKey, out Brush brush)
    {
        if (TryFindResource(resourceKey) is Brush resourceBrush)
        {
            brush = resourceBrush;
            return true;
        }

        brush = Brushes.Transparent;
        return false;
    }

    private static object? TryFindResource(string resourceKey)
    {
        return Application.Current?.TryFindResource(resourceKey);
    }

    private static bool TryGetColor(object? value, out Color color)
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

    private static double GetRelativeLuminance(Color color)
    {
        var red = GetLinearChannel(color.R);
        var green = GetLinearChannel(color.G);
        var blue = GetLinearChannel(color.B);

        return 0.2126d * red + 0.7152d * green + 0.0722d * blue;
    }

    private static double GetLinearChannel(byte channel)
    {
        var normalized = channel / 255d;
        return normalized <= 0.03928d
            ? normalized / 12.92d
            : Math.Pow((normalized + 0.055d) / 1.055d, 2.4d);
    }
}
