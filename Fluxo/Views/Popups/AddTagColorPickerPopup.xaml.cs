using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Fluxo.Views.CustomControls;

namespace Fluxo.Views.Popups;

public partial class AddTagColorPickerPopup : BasePopup
{
    private bool _isDraggingColorSurface;
    private bool _isUpdatingUi;
    private double _hue;
    private double _saturation;
    private double _value;

    public AddTagColorPickerPopup(string initialHexColor)
    {
        InitializeComponent();

        InitializeFromHex(initialHexColor);
        Loaded += (_, _) => UpdateUi();
    }

    public string SelectedHexColor { get; private set; } = "#3FE0A1";

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        if (!TryParseHexColor(HexCodeTextBox.Text, out _))
            return;

        SelectedHexColor = NormalizeHex(HexCodeTextBox.Text);
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnHueSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingUi)
            return;

        _hue = e.NewValue;
        UpdateUi();
    }

    private void OnHexCodeTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_isUpdatingUi || !TryParseHexColor(HexCodeTextBox.Text, out var color))
            return;

        ColorToHsv(color, out _hue, out _saturation, out _value);
        UpdateUi();
    }

    private void OnSaturationSurfaceMouseDown(object sender, MouseButtonEventArgs e)
    {
        UpdateSaturationValueFromPoint(e.GetPosition(SaturationValueSurface));
    }

    private void OnSaturationSurfaceMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingColorSurface)
            return;

        UpdateSaturationValueFromPoint(e.GetPosition(SaturationValueSurface));
    }

    private void OnSaturationSurfaceMouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingColorSurface = false;
        SaturationValueSurface.ReleaseMouseCapture();
    }

    private void OnSaturationSurfaceMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingColorSurface = true;
        SaturationValueSurface.CaptureMouse();
        UpdateSaturationValueFromPoint(e.GetPosition(SaturationValueSurface));
    }

    private void OnSaturationSurfaceMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingColorSurface = false;
        SaturationValueSurface.ReleaseMouseCapture();
    }

    private void UpdateSaturationValueFromPoint(Point point)
    {
        var width = Math.Max(1, SaturationValueSurface.ActualWidth);
        var height = Math.Max(1, SaturationValueSurface.ActualHeight);

        _saturation = Math.Clamp(point.X / width, 0, 1);
        _value = Math.Clamp(1 - (point.Y / height), 0, 1);
        UpdateUi();
    }

    private void InitializeFromHex(string hexCode)
    {
        if (!TryParseHexColor(hexCode, out var color))
            color = (Color)ColorConverter.ConvertFromString("#3FE0A1");

        ColorToHsv(color, out _hue, out _saturation, out _value);
    }

    private void UpdateUi()
    {
        _isUpdatingUi = true;
        try
        {
            var hueColor = HsvToColor(_hue, 1, 1);
            HuePreviewSurface.Background = new SolidColorBrush(hueColor);
            HueSlider.Value = _hue;

            var finalColor = HsvToColor(_hue, _saturation, _value);
            var hex = $"#{finalColor.R:X2}{finalColor.G:X2}{finalColor.B:X2}";
            SelectedHexColor = hex;
            HexCodeTextBox.Text = hex;
            ColorHandle.Fill = new SolidColorBrush(finalColor);

            var width = Math.Max(1, SaturationValueSurface.ActualWidth);
            var height = Math.Max(1, SaturationValueSurface.ActualHeight);
            var x = (_saturation * width) - (ColorHandle.Width / 2);
            var y = ((1 - _value) * height) - (ColorHandle.Height / 2);

            var leftMin = -1d;
            var topMin = -1d;
            var leftMax = Math.Max(leftMin, width - ColorHandle.Width + 1);
            var topMax = Math.Max(topMin, height - ColorHandle.Height + 1);

            System.Windows.Controls.Canvas.SetLeft(ColorHandle, Math.Clamp(x, leftMin, leftMax));
            System.Windows.Controls.Canvas.SetTop(ColorHandle, Math.Clamp(y, topMin, topMax));
        }
        finally
        {
            _isUpdatingUi = false;
        }
    }

    private static string NormalizeHex(string text)
    {
        var normalized = (text ?? string.Empty).Trim().TrimStart('#').ToUpperInvariant();
        return normalized.Length == 6 ? $"#{normalized}" : "#3FE0A1";
    }

    private static bool TryParseHexColor(string? text, out Color color)
    {
        color = default;
        var normalized = (text ?? string.Empty).Trim();
        if (normalized.StartsWith('#'))
            normalized = normalized[1..];

        if (normalized.Length != 6 || normalized.Any(character =>
                !char.IsDigit(character) &&
                (character < 'A' || character > 'F') &&
                (character < 'a' || character > 'f')))
            return false;

        color = (Color)ColorConverter.ConvertFromString($"#{normalized.ToUpperInvariant()}");
        return true;
    }

    private static Color HsvToColor(double hue, double saturation, double value)
    {
        hue = (hue % 360 + 360) % 360;
        var chroma = value * saturation;
        var x = chroma * (1 - Math.Abs((hue / 60 % 2) - 1));
        var m = value - chroma;

        var (redPrime, greenPrime, bluePrime) = hue switch
        {
            < 60 => (chroma, x, 0d),
            < 120 => (x, chroma, 0d),
            < 180 => (0d, chroma, x),
            < 240 => (0d, x, chroma),
            < 300 => (x, 0d, chroma),
            _ => (chroma, 0d, x)
        };

        return Color.FromRgb(
            Convert.ToByte(Math.Round((redPrime + m) * 255)),
            Convert.ToByte(Math.Round((greenPrime + m) * 255)),
            Convert.ToByte(Math.Round((bluePrime + m) * 255)));
    }

    private static void ColorToHsv(Color color, out double hue, out double saturation, out double value)
    {
        var red = color.R / 255d;
        var green = color.G / 255d;
        var blue = color.B / 255d;

        var max = Math.Max(red, Math.Max(green, blue));
        var min = Math.Min(red, Math.Min(green, blue));
        var delta = max - min;

        hue = delta == 0
            ? 0
            : max == red
                ? 60 * (((green - blue) / delta) % 6)
                : max == green
                    ? 60 * (((blue - red) / delta) + 2)
                    : 60 * (((red - green) / delta) + 4);
        if (hue < 0)
            hue += 360;

        saturation = max <= 0 ? 0 : delta / max;
        value = max;
    }
}
