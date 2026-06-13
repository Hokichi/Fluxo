using System.IO;
using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Shell.Main;

public sealed class SavingGoalsPanelLayoutTests
{
    private static readonly string SavingGoalsPanelXamlPath = RepositoryPaths.File(
        "Fluxo",
        "Views",
        "Shell",
        "Main",
        "Sections",
        "SavingGoalsPanel.xaml");

    [Fact]
    public void BottomActionBarExposesNavigationAndAddAction()
    {
        var xaml = File.ReadAllText(SavingGoalsPanelXamlPath);

        Assert.Contains("OnAddSavingGoalClick", xaml);
        Assert.Contains("CircleWithPlus", xaml);
        Assert.DoesNotContain("PlusSolid", xaml);
        Assert.Contains("SavingGoalActionBalloonButtonStyle", xaml);
        Assert.Contains("OnNavigatePreviousClick", xaml);
        Assert.Contains("OnNavigateNextClick", xaml);
        Assert.Contains("HasMultipleSavingGoals", xaml);
        Assert.Contains("Grid.Column=\"0\"", xaml);
        Assert.Contains("Grid.Column=\"1\"", xaml);
        Assert.Contains("Grid.Column=\"2\"", xaml);
    }

    [Fact]
    public void SavingGoalTemplateExposesCompactMetricsAndActions()
    {
        var xaml = File.ReadAllText(SavingGoalsPanelXamlPath);

        Assert.Contains("Amount Left", xaml);
        Assert.Contains("Weekly Average", xaml);
        Assert.Contains("Estimated Deadline", xaml);
        Assert.Contains("OnEditSavingGoalClick", xaml);
        Assert.Contains("OnAddGoalFundsClick", xaml);
        Assert.Contains("ButtonText=\"Edit Goal\"", xaml);
        Assert.Contains("ButtonText=\"Add Funds\"", xaml);
        Assert.DoesNotContain(">saved<", xaml);
        Assert.DoesNotContain("target", xaml);
    }
}
