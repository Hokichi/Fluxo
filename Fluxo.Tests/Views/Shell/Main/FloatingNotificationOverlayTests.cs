using Fluxo.Tests.TestSupport;
using Fluxo.Views.Shell.Main;
using Xunit;

namespace Fluxo.Tests.Views.Shell.Main;

public sealed class FloatingNotificationOverlayTests
{
    [Theory]
    [InlineData(42u, 42, true)]
    [InlineData(7u, 42, false)]
    [InlineData(0u, 42, false)]
    public void IsForegroundProcess_MatchesCurrentProcess(
        uint foregroundProcessId, int currentProcessId, bool expected)
    {
        Assert.Equal(expected,
            FloatingNotificationOverlayWindow.IsForegroundProcess(foregroundProcessId, currentProcessId));
    }

    [Fact]
    public void List_UsesSeverityBorderAndBackgroundWithoutAccentBar()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "Sections", "FloatingNotificationList.xaml"));
        Assert.Contains("Width=\"360\"", xaml);
        Assert.Contains("MaxHeight=\"243\"", xaml);
        Assert.Contains("BorderThickness=\"2\"", xaml);
        Assert.Contains("x:Name=\"AccentBackground\"", xaml);
        Assert.Contains("Opacity=\"0.5\"", xaml);
        Assert.Contains("ElementName=AccentBackground", xaml);
        Assert.DoesNotContain("<ColumnDefinition Width=\"5\" />", xaml);
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
