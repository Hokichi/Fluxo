using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Fluxo.Resources.Converters;

public sealed class ForegroundForBackgroundBrushConverter : IValueConverter, IMultiValueConverter
{
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

        var targetForeground = TryFindDarkTextBrush(currentForeground, out var darkForeground)
            ? darkForeground
            : ResolveBrush("Brush.Text.Primary.Dark", currentForeground);

        if (!TryGetColor(currentForeground, out var foregroundColor) ||
            !TryGetColor(targetForeground, out var targetColor))
        {
            return targetForeground;
        }

        var backgroundLuminance = GetRelativeLuminance(backgroundColor);
        var computedForeground = new SolidColorBrush(
            InterpolateColor(
                foregroundColor,
                targetColor,
                backgroundLuminance,
                InvertAlpha(backgroundColor.A)));

        if (computedForeground.CanFreeze)
            computedForeground.Freeze();

        return computedForeground;
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

    private static Color InterpolateColor(Color foregroundColor, Color targetColor, double amount, byte alpha)
    {
        var clampedAmount = Math.Clamp(amount, 0d, 1d);
        return Color.FromArgb(
            alpha,
            InterpolateByte(foregroundColor.R, targetColor.R, clampedAmount),
            InterpolateByte(foregroundColor.G, targetColor.G, clampedAmount),
            InterpolateByte(foregroundColor.B, targetColor.B, clampedAmount));
    }

    private static byte InterpolateByte(byte start, byte end, double amount)
    {
        return (byte)Math.Clamp(Math.Round(start + (end - start) * amount), 0d, 255d);
    }

    private static byte InvertAlpha(byte alpha)
    {
        return (byte)(byte.MaxValue - alpha);
    }
}
