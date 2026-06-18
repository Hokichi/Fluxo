using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Shell.Main;

public sealed class MainWindowMaximizeBoundsTests
{
    [Fact]
    public void Maximize_UsesFullMonitorBoundsSoAutoHiddenTaskbarCanReveal()
    {
        var source = File.ReadAllText(RepositoryPaths.File(
            "Fluxo",
            "Views",
            "Shell",
            "Main",
            "MainWindow.xaml.cs"));

        Assert.Contains("private Rect GetMonitorBounds()", source);
        Assert.Contains("var maximizedBounds = GetMonitorBounds();", source);
        Assert.Contains("_currentBounds = maximizedBounds;", source);
        Assert.Contains("AnimateStateChange(from, maximizedBounds, true);", source);
        Assert.Contains("info.rcMonitor.Left", source);
        Assert.Contains("info.rcMonitor.Bottom - info.rcMonitor.Top", source);
    }
}
