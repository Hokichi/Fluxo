using System.IO;
using Xunit;

namespace Fluxo.Tests.Views.Shell.Main;

public sealed class MainWindowLayoutTests
{
    private static readonly string MainWindowXamlPath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "..",
        "Fluxo",
        "Views",
        "Shell",
        "Main",
        "MainWindow.xaml"));

    [Fact]
    public void HeaderMenu_DoesNotExposeAnalyticsActionButton()
    {
        var xaml = File.ReadAllText(MainWindowXamlPath);

        Assert.DoesNotContain("Click=\"OnAnalyticsButtonClick\"", xaml);
    }

    [Fact]
    public void AnalyticsDrawerTabTrigger_RemainsAvailable()
    {
        var xaml = File.ReadAllText(MainWindowXamlPath);

        Assert.Contains("Click=\"OnAnalyticsDrawerTabClick\"", xaml);
    }
}
