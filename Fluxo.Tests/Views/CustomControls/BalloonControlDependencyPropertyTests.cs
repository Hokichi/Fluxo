using System.Windows;
using Fluxo.Resources.CustomControls;
using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.CustomControls;

public sealed class BalloonControlDependencyPropertyTests
{
    [Fact]
    public void BalloonButton_InheritsSharedBalloonControl()
    {
        Assert.True(typeof(BalloonControl).IsAssignableFrom(typeof(BalloonButton)));
        Assert.Equal(typeof(BalloonControl), BalloonControl.ButtonTextProperty.OwnerType);
        Assert.Equal(typeof(BalloonControl), BalloonControl.DefaultBackgroundProperty.OwnerType);
        Assert.Null(typeof(BalloonControl).GetProperty("ActiveBackground"));
        Assert.Null(typeof(BalloonButton).GetProperty("ActiveBackground"));
    }

    [Fact]
    public void BalloonControl_CalculatesAutoOpenWidth()
    {
        Assert.Equal(88, BalloonControl.CalculateAutoOpenWidth(
            28,
            8,
            new Thickness(6, 0, 10, 0),
            52,
            new Thickness(8, 0, 0, 0)));
    }

    [Fact]
    public void BalloonControl_SwitchesHoverFillWithoutColorAnimation()
    {
        var source = File.ReadAllText(RepositoryPaths.File(
            "Fluxo.Resources", "CustomControls", "BalloonControl.cs"));

        Assert.Contains("SetShapeFill(ResolveHoveredBackground());", source);
        Assert.Equal(2, source.Split(
            "SetShapeFill(ResolveRestingBackground());",
            StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("ColorAnimation", source);
    }
}
