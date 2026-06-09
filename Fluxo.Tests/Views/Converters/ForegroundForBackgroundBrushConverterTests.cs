using Fluxo.Resources.Converters;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Xunit;

namespace Fluxo.Tests.Views.Converters;

public sealed class ForegroundForBackgroundBrushConverterTests
{
    [Fact]
    public void Convert_ReturnsDarkPrimaryBrush_WhenBackgroundHexIsBright_AndForegroundIsPrimary()
    {
        using var appScope = new ApplicationResourceScope();
        var converter = new ForegroundForBackgroundBrushConverter();

        var result = converter.Convert(
            "#E6EAF0",
            typeof(Brush),
            appScope.PrimaryBrush,
            CultureInfo.InvariantCulture);

        Assert.Same(appScope.PrimaryDarkBrush, result);
    }

    [Fact]
    public void Convert_ReturnsCurrentForeground_WhenBackgroundHexIsDark()
    {
        using var appScope = new ApplicationResourceScope();
        var converter = new ForegroundForBackgroundBrushConverter();

        var result = converter.Convert(
            "#121417",
            typeof(Brush),
            appScope.PrimaryBrush,
            CultureInfo.InvariantCulture);

        Assert.Same(appScope.PrimaryBrush, result);
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
    public void Convert_ReturnsDarkSecondaryBrush_WhenBackgroundBrushIsBright_AndForegroundIsSecondary()
    {
        using var appScope = new ApplicationResourceScope();
        var converter = new ForegroundForBackgroundBrushConverter();

        var result = converter.Convert(
            new SolidColorBrush(Color.FromRgb(255, 176, 32)),
            typeof(Brush),
            appScope.SecondaryBrush,
            CultureInfo.InvariantCulture);

        Assert.Same(appScope.SecondaryDarkBrush, result);
    }

    [Fact]
    public void Convert_ReturnsPrimaryDarkBrush_WhenBackgroundIsBright_AndNoForegroundIsProvided()
    {
        using var appScope = new ApplicationResourceScope();
        var converter = new ForegroundForBackgroundBrushConverter();

        var result = converter.Convert(
            Brushes.White,
            typeof(Brush),
            null!,
            CultureInfo.InvariantCulture);

        Assert.Same(appScope.PrimaryDarkBrush, result);
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