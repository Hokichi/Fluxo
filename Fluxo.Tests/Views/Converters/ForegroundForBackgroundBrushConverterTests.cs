using Fluxo.Resources.Converters;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Xunit;

namespace Fluxo.Tests.Views.Converters;

public sealed class ForegroundForBackgroundBrushConverterTests
{
    [Fact]
    public void Convert_ReturnsComputedPrimaryForeground_WhenBackgroundHexIsBright_AndForegroundIsPrimary()
    {
        using var appScope = new ApplicationResourceScope();
        var converter = new ForegroundForBackgroundBrushConverter();

        var result = converter.Convert(
            "#FFFFFF",
            typeof(Brush),
            appScope.PrimaryBrush,
            CultureInfo.InvariantCulture);

        var brush = Assert.IsType<SolidColorBrush>(result);
        AssertColorEqual(Color.FromArgb(0, appScope.PrimaryDarkBrush.Color.R, appScope.PrimaryDarkBrush.Color.G, appScope.PrimaryDarkBrush.Color.B), brush.Color);
    }

    [Fact]
    public void Convert_ReturnsCurrentForegroundColor_WhenBackgroundHexIsBlack()
    {
        using var appScope = new ApplicationResourceScope();
        var converter = new ForegroundForBackgroundBrushConverter();

        var result = converter.Convert(
            "#000000",
            typeof(Brush),
            appScope.PrimaryBrush,
            CultureInfo.InvariantCulture);

        var brush = Assert.IsType<SolidColorBrush>(result);
        AssertColorEqual(Color.FromArgb(0, appScope.PrimaryBrush.Color.R, appScope.PrimaryBrush.Color.G, appScope.PrimaryBrush.Color.B), brush.Color);
    }

    [Fact]
    public void Convert_ReturnsPrimaryBrush_WhenBackgroundCannotBeParsed_AndNoForegroundIsProvided()
    {
        using var appScope = new ApplicationResourceScope();
        var converter = new ForegroundForBackgroundBrushConverter();

        var result = converter.Convert(
            "not-a-color",
            typeof(Brush),
            null!,
            CultureInfo.InvariantCulture);

        Assert.Same(appScope.PrimaryBrush, result);
    }

    [Fact]
    public void Convert_ReturnsInterpolatedBrush_WhenBackgroundBrushIsMidTone_AndForegroundIsSecondary()
    {
        using var appScope = new ApplicationResourceScope();
        var converter = new ForegroundForBackgroundBrushConverter();

        var result = converter.Convert(
            new SolidColorBrush(Color.FromRgb(128, 128, 128)),
            typeof(Brush),
            appScope.SecondaryBrush,
            CultureInfo.InvariantCulture);

        var brush = Assert.IsType<SolidColorBrush>(result);
        Assert.Equal(0, brush.Color.A);
        Assert.NotEqual(appScope.SecondaryBrush.Color, brush.Color);
        Assert.NotEqual(appScope.SecondaryDarkBrush.Color, brush.Color);
        Assert.InRange(brush.Color.R, appScope.SecondaryDarkBrush.Color.R + 1, appScope.SecondaryBrush.Color.R - 1);
        Assert.InRange(brush.Color.G, appScope.SecondaryDarkBrush.Color.G + 1, appScope.SecondaryBrush.Color.G - 1);
        Assert.InRange(brush.Color.B, appScope.SecondaryDarkBrush.Color.B + 1, appScope.SecondaryBrush.Color.B - 1);
    }

    [Fact]
    public void Convert_ReturnsComputedPrimaryDarkColor_WhenBackgroundIsBright_AndNoForegroundIsProvided()
    {
        using var appScope = new ApplicationResourceScope();
        var converter = new ForegroundForBackgroundBrushConverter();

        var result = converter.Convert(
            Brushes.White,
            typeof(Brush),
            null!,
            CultureInfo.InvariantCulture);

        var brush = Assert.IsType<SolidColorBrush>(result);
        AssertColorEqual(Color.FromArgb(0, appScope.PrimaryDarkBrush.Color.R, appScope.PrimaryDarkBrush.Color.G, appScope.PrimaryDarkBrush.Color.B), brush.Color);
    }

    [Fact]
    public void Convert_InvertsBackgroundAlpha_ForComputedForeground()
    {
        using var appScope = new ApplicationResourceScope();
        var converter = new ForegroundForBackgroundBrushConverter();

        var result = converter.Convert(
            new SolidColorBrush(Color.FromArgb(64, 255, 255, 255)),
            typeof(Brush),
            appScope.PrimaryBrush,
            CultureInfo.InvariantCulture);

        var brush = Assert.IsType<SolidColorBrush>(result);
        Assert.Equal(191, brush.Color.A);
    }

    private static void AssertColorEqual(Color expected, Color actual)
    {
        Assert.Equal(expected.A, actual.A);
        Assert.Equal(expected.R, actual.R);
        Assert.Equal(expected.G, actual.G);
        Assert.Equal(expected.B, actual.B);
    }

    private sealed class ApplicationResourceScope : IDisposable
    {
        private readonly Application? _previousApplication;
        private readonly bool _ownsApplication;

        public ApplicationResourceScope()
        {
            _previousApplication = Application.Current;
            if (_previousApplication is null)
            {
                _ = new Application();
                _ownsApplication = true;
            }

            PrimaryBrush = new SolidColorBrush(Color.FromRgb(0xE6, 0xEA, 0xF0));
            SecondaryBrush = new SolidColorBrush(Color.FromRgb(0x9B, 0xA3, 0xAE));
            MutedBrush = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));
            PrimaryDarkBrush = new SolidColorBrush(Color.FromRgb(0x1A, 0x1D, 0x21));
            SecondaryDarkBrush = new SolidColorBrush(Color.FromRgb(0x20, 0x24, 0x2A));
            MutedDarkBrush = new SolidColorBrush(Color.FromRgb(0x23, 0x28, 0x30));

            Application.Current.Resources["Brush.Text.Primary"] = PrimaryBrush;
            Application.Current.Resources["Brush.Text.Secondary"] = SecondaryBrush;
            Application.Current.Resources["Brush.Text.Muted"] = MutedBrush;
            Application.Current.Resources["Brush.Text.Primary.Dark"] = PrimaryDarkBrush;
            Application.Current.Resources["Brush.Text.Secondary.Dark"] = SecondaryDarkBrush;
            Application.Current.Resources["Brush.Text.Muted.Dark"] = MutedDarkBrush;
        }

        public SolidColorBrush PrimaryBrush { get; }

        public SolidColorBrush SecondaryBrush { get; }

        public SolidColorBrush MutedBrush { get; }

        public SolidColorBrush PrimaryDarkBrush { get; }

        public SolidColorBrush SecondaryDarkBrush { get; }

        public SolidColorBrush MutedDarkBrush { get; }

        public void Dispose()
        {
            Application.Current.Resources.Clear();

            if (_ownsApplication)
                Application.Current.Shutdown();
        }
    }
}
