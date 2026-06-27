using System.Windows;
using Fluxo.Resources.CustomControls;
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
}
