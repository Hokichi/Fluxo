using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Shell.Main;

public sealed class FloatingNotificationOverlayTests
{
    [Fact]
    public void List_UsesThreeCardViewportAndOneWayBindings()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "Sections", "FloatingNotificationList.xaml"));
        Assert.Contains("Width=\"400\"", xaml);
        Assert.Contains("MaxHeight=\"540\"", xaml);
        Assert.Contains("Mode=OneWay", xaml);
        Assert.Contains("NotificationAccentStyle", xaml);
    }

    [Fact]
    public void Overlay_IsTransparentAndNonActivating()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "FloatingNotificationOverlayWindow.xaml"));
        Assert.Contains("AllowsTransparency=\"True\"", xaml);
        Assert.Contains("ShowActivated=\"False\"", xaml);
        Assert.Contains("Topmost=\"True\"", xaml);
    }
}
