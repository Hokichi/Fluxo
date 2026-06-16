using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Styles;

public sealed class BalloonCheckBoxStyleTests
{
    [Fact]
    public void BalloonCheckBoxDefaultStyle_UsesBalloonChromeAndMintCheckedBackground()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo.Resources", "Resources", "Styles", "ButtonStyles.xaml"));

        Assert.Contains("<Style TargetType=\"{x:Type c:BalloonCheckBox}\">", xaml);
        Assert.Contains("<Setter Property=\"CheckedBackground\" Value=\"{StaticResource Brush.Mint}\" />", xaml);
        Assert.Contains("ControlTemplate TargetType=\"{x:Type c:BalloonCheckBox}\"", xaml);
        Assert.Contains("x:Name=\"PART_Icon\"", xaml);
        Assert.Contains("Path=\"ActiveBackground\"", xaml);
    }
}
