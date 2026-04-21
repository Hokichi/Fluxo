using System.IO;
using Xunit;

namespace Fluxo.Tests.Views.Shell.Main;

public sealed class SavingGoalsPanelLayoutTests
{
    private static readonly string SavingGoalsPanelXamlPath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "..",
        "Fluxo",
        "Views",
        "Shell",
        "Main",
        "Sections",
        "SavingGoalsPanel.xaml"));

    [Fact]
    public void HeaderUsesBalloonNavigationAndDoesNotExposeAddAction()
    {
        var xaml = File.ReadAllText(SavingGoalsPanelXamlPath);

        Assert.DoesNotContain("OnAddSavingGoalClick", xaml);
        Assert.Contains("SavingGoalHeaderNavBalloonButtonStyle", xaml);
        Assert.Contains("OnNavigatePreviousClick", xaml);
        Assert.Contains("OnNavigateNextClick", xaml);
        Assert.Contains("HasMultipleSavingGoals", xaml);
    }
}
