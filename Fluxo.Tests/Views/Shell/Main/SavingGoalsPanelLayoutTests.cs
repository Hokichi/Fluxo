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
    public void BottomActionBarExposesNavigationAndAddAction()
    {
        var xaml = File.ReadAllText(SavingGoalsPanelXamlPath);

        Assert.Contains("OnAddSavingGoalClick", xaml);
        Assert.Contains("SavingGoalActionBalloonButtonStyle", xaml);
        Assert.Contains("OnNavigatePreviousClick", xaml);
        Assert.Contains("OnNavigateNextClick", xaml);
        Assert.Contains("HasMultipleSavingGoals", xaml);
    }
}
