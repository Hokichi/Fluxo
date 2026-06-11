using System.Text.RegularExpressions;
using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Styles;

public sealed class BalloonButtonStyleTests
{
    [Fact]
    public void BalloonButtonDefaultStyle_BindsDimensionsToButtonSize()
    {
        var style = ReadDefaultBalloonButtonStyle();

        Assert.Contains("Property=\"Width\" Value=\"{Binding RelativeSource={RelativeSource Self}, Path=ButtonSize}\"", style);
        Assert.Contains("Property=\"Height\" Value=\"{Binding RelativeSource={RelativeSource Self}, Path=ButtonSize}\"", style);
    }

    [Fact]
    public void BalloonButtonDefaultTemplate_BindsIconAndRightSideText()
    {
        var style = ReadDefaultBalloonButtonStyle();

        Assert.Contains("x:Name=\"PART_Icon\"", style);
        Assert.Contains("x:Name=\"PART_TextReveal\"", style);
        Assert.Contains("x:Name=\"PART_ButtonText\"", style);
        Assert.Contains("Text=\"{TemplateBinding ButtonText}\"", style);
        Assert.Matches(
            new Regex("PART_Icon[\\s\\S]*PART_TextReveal[\\s\\S]*PART_ButtonText", RegexOptions.Singleline),
            style);
    }

    [Fact]
    public void BalloonButtonDefaultTemplate_BindsIconAndTextForegroundToBackgroundBrightness()
    {
        var style = ReadDefaultBalloonButtonStyle();

        Assert.Contains("Converter=\"{StaticResource ForegroundForBackgroundBrushConverter}\"", style);
        Assert.Contains("Path=\"ActiveBackground\" RelativeSource=\"{RelativeSource TemplatedParent}\"", style);
        Assert.Contains("Path=\"Foreground\" RelativeSource=\"{RelativeSource TemplatedParent}\"", style);
        Assert.Contains("<Path.Fill>", style);
        Assert.Contains("<TextBlock.Foreground>", style);
    }

    [Fact]
    public void BalloonButtonDefaultTemplate_LeavesExpansionAnimationToCodeBehind()
    {
        var style = ReadDefaultBalloonButtonStyle();

        Assert.DoesNotContain("<BeginStoryboard>", style);
        Assert.DoesNotContain("To=\"{Binding", style);
        Assert.Contains("x:Name=\"PART_TextReveal\"", style);
        Assert.Contains("x:Name=\"PART_ButtonText\"", style);
    }

    [Fact]
    public void MainWindowBalloonButtonChromeStyle_SetsCompactButtonSize()
    {
        var xaml = ReadButtonStylesXaml();
        var styleStart = xaml.IndexOf("x:Key=\"MainWindowBalloonButtonChromeStyle\"", StringComparison.Ordinal);
        Assert.True(styleStart >= 0);

        var nextStyleStart = xaml.IndexOf("<Style TargetType=\"{x:Type c:SwipeRevealContainer}\"", styleStart, StringComparison.Ordinal);
        Assert.True(nextStyleStart > styleStart);

        var style = xaml[styleStart..nextStyleStart];

        Assert.Contains("<Setter Property=\"ButtonSize\" Value=\"28\" />", style);
        Assert.Contains("<Setter Property=\"ExpandedWidth\" Value=\"96\" />", style);
        Assert.DoesNotContain("<Setter Property=\"Padding\"", style);
    }

    [Fact]
    public void DetailActionBalloonButtonStyle_UsesButtonSizeForSquareOverride()
    {
        var xaml = ReadButtonStylesXaml();
        var styleStart = xaml.IndexOf("x:Key=\"DetailActionBalloonButtonStyle\"", StringComparison.Ordinal);
        Assert.True(styleStart >= 0);

        var nextStyleStart = xaml.IndexOf("<Style x:Key=\"SourceButtonStyle\"", styleStart, StringComparison.Ordinal);
        Assert.True(nextStyleStart > styleStart);

        var style = xaml[styleStart..nextStyleStart];

        Assert.Contains("<Setter Property=\"ButtonSize\" Value=\"40\" />", style);
        Assert.DoesNotContain("<Setter Property=\"Width\" Value=\"40\" />", style);
    }

    private static string ReadDefaultBalloonButtonStyle()
    {
        var xaml = ReadButtonStylesXaml();
        var styleStart = xaml.IndexOf("<Style TargetType=\"{x:Type c:BalloonButton}\">", StringComparison.Ordinal);
        Assert.True(styleStart >= 0, "Could not find the default BalloonButton style start.");

        var nextStyleStart = xaml.IndexOf("x:Key=\"MainWindowBalloonButtonChromeStyle\"", styleStart, StringComparison.Ordinal);
        Assert.True(nextStyleStart > styleStart, "Could not find the next BalloonButton chrome style.");

        return xaml[styleStart..nextStyleStart];
    }

    private static string ReadButtonStylesXaml() =>
        File.ReadAllText(RepositoryPaths.File("Fluxo.Resources", "Resources", "Styles", "ButtonStyles.xaml"));
}
