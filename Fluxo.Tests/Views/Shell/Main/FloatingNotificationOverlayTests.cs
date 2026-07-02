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
    public void Overlay_IsTransparentAndNonActivating()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "FloatingNotificationOverlayWindow.xaml"));
        Assert.Contains("AllowsTransparency=\"True\"", xaml);
        Assert.Contains("ShowActivated=\"False\"", xaml);
        Assert.Contains("Topmost=\"True\"", xaml);
    }
}
