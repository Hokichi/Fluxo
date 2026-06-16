using System.IO;
using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Shell.Main;

public sealed class SpentAllowancePanelLayoutTests
{
    private static readonly string SpentAllowancePanelXamlPath = RepositoryPaths.File(
        "Fluxo",
        "Views",
        "Shell",
        "Main",
        "Sections",
        "SpentAllowancePanel.xaml");

    [Fact]
    public void PanelShowsNetCardBoundToDynamicNetValue()
    {
        var xaml = File.ReadAllText(SpentAllowancePanelXamlPath);

        Assert.Contains("Text=\"NET\"", xaml);
        Assert.Contains("Text=\"{Binding Net, Converter={StaticResource NumberWithCommasConverter}}\"", xaml);
        Assert.Contains("ToolTip=\"{Binding Net, Converter={StaticResource MoneyFullDisplayConverter}}\"", xaml);
        Assert.Contains("Foreground=\"{Binding Net, Converter={StaticResource DifferenceToBrushConverter}}\"", xaml);
    }

    [Fact]
    public void EarnedCard_BindsToTotalEarned()
    {
        var xaml = File.ReadAllText(SpentAllowancePanelXamlPath);
        var earnedIndex = xaml.IndexOf("Text=\"EARNED\"", StringComparison.Ordinal);
        var netIndex = xaml.IndexOf("Text=\"NET\"", StringComparison.Ordinal);
        var earnedCard = xaml[earnedIndex..netIndex];

        Assert.Contains("Text=\"{Binding TotalEarned, Converter={StaticResource NumberWithCommasConverter}}\"", earnedCard);
        Assert.Contains("ToolTip=\"{Binding TotalEarned, Converter={StaticResource MoneyFullDisplayConverter}}\"", earnedCard);
    }
}
