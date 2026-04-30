using Fluxo.Views.Shell.Tray;
using Xunit;
using DPoint = System.Drawing.Point;
using DRectangle = System.Drawing.Rectangle;
using WPoint = System.Windows.Point;
using WRect = System.Windows.Rect;
using WSize = System.Windows.Size;

namespace Fluxo.Tests.Views.Shell.Tray;

public sealed class StartupNotificationPopupPlacementTests
{
    [Fact]
    public void CalculatePopupOrigin_UsesExpectedTrayAnchoredPlacement_WhenWithinWorkAreaBounds()
    {
        var anchorPoint = new WPoint(500, 600);
        var popupSize = new WSize(320, 120);
        var workArea = new WRect(0, 0, 1920, 1080);

        var origin = StartupNotificationPopup.CalculatePopupOrigin(anchorPoint, popupSize, workArea);

        Assert.Equal(192, origin.X);
        Assert.Equal(468, origin.Y);
    }

    [Fact]
    public void CalculatePopupOrigin_UsesSafeClamp_WhenPopupExceedsWorkArea()
    {
        var anchorPoint = new WPoint(500, 600);
        var popupSize = new WSize(2500, 1400);
        var workArea = new WRect(0, 0, 1920, 1080);

        var origin = StartupNotificationPopup.CalculatePopupOrigin(anchorPoint, popupSize, workArea);

        Assert.Equal(0, origin.X);
        Assert.Equal(0, origin.Y);
    }

    [Fact]
    public void ResolveWorkAreaInDip_UsesMonitorSpecificResolverPath_AndConvertsFromDevicePixels()
    {
        var invokedWithPoint = DPoint.Empty;
        var workAreaDip = StartupNotificationPopup.ResolveWorkAreaInDip(
            new WPoint(2500.4, 405.6),
            1.25,
            1.25,
            point =>
            {
                invokedWithPoint = point;
                return point.X >= 2000
                    ? new DRectangle(1920, 0, 1920, 1040)
                    : new DRectangle(0, 0, 1920, 1080);
            });

        Assert.Equal(new DPoint(2500, 406), invokedWithPoint);
        Assert.Equal(1536, workAreaDip.Left);
        Assert.Equal(0, workAreaDip.Top);
        Assert.Equal(1536, workAreaDip.Width);
        Assert.Equal(832, workAreaDip.Height);
    }
}
