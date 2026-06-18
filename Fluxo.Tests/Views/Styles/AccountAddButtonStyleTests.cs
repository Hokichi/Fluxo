using System.IO;
using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Styles;

public sealed class AccountAddButtonStyleTests
{
    [Fact]
    public void AccountAddButtonStyle_UsesButtonContentForLabel()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File(
            "Fluxo.Resources",
            "Resources",
            "Styles",
            "ButtonStyles.xaml"));

        Assert.Contains("x:Key=\"AccountAddButtonStyle\"", xaml);
        Assert.Contains("Text=\"{TemplateBinding Content}\"", xaml);
    }

    [Fact]
    public void AccountAddButtonStyle_DoesNotScaleHoverBackgroundBehindDashedBorder()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File(
            "Fluxo.Resources",
            "Resources",
            "Styles",
            "ButtonStyles.xaml"));

        var styleStart = xaml.IndexOf("x:Key=\"AccountAddButtonStyle\"", StringComparison.Ordinal);
        Assert.True(styleStart >= 0);

        var nextStyleStart = xaml.IndexOf("<Style x:Key=\"MoveToCurrentPeriodButtonStyle\"", styleStart, StringComparison.Ordinal);
        Assert.True(nextStyleStart > styleStart);

        var styleSection = xaml[styleStart..nextStyleStart];

        Assert.Contains("x:Name=\"HoverBackground\"", styleSection);
        Assert.Contains("Opacity=\"0\"", styleSection);
        Assert.DoesNotContain("ScaleTransform", styleSection);
        Assert.DoesNotContain("ScaleTransform.ScaleX", styleSection);
        Assert.DoesNotContain("ScaleTransform.ScaleY", styleSection);
    }
}
