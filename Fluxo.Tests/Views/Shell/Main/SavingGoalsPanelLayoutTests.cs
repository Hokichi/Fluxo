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

    private static readonly string SavingGoalsPanelCodeBehindPath = RepositoryPaths.File(
        "Fluxo",
        "Views",
        "Shell",
        "Main",
        "Sections",
        "SavingGoalsPanel.xaml.cs");

    [Fact]
    public void PanelExposesNavigationAndAddAction()
    {
        var xaml = File.ReadAllText(SavingGoalsPanelXamlPath);

        Assert.Contains("customControls:StepNavigatorControl", xaml);
        Assert.Contains("CurrentStep=\"{Binding CurrentStepNumber}\"", xaml);
        Assert.Contains("StepCount=\"{Binding GoalStepCount}\"", xaml);
        Assert.Contains("ShouldIndicateProgress=\"False\"", xaml);
        Assert.Contains("ShouldPaginate=\"True\"", xaml);
        Assert.DoesNotContain("GoalDots", xaml);
        Assert.Contains("OnAddSavingGoalClick", xaml);
        Assert.Contains("PlusSolid", xaml);
        Assert.Contains("SavingGoalActionBalloonButtonStyle", xaml);
        Assert.Contains("OnNavigatePreviousClick", xaml);
        Assert.Contains("OnNavigateNextClick", xaml);
        Assert.Contains("HasMultipleSavingGoals", xaml);
        Assert.Contains("Grid.Column=\"0\"", xaml);
        Assert.Contains("Grid.Column=\"1\"", xaml);
        Assert.Contains("Grid.Column=\"2\"", xaml);
    }

    [Fact]
    public void HeaderGoalActionsUseCurrentGoal()
    {
        var source = File.ReadAllText(SavingGoalsPanelCodeBehindPath);

        Assert.Contains("var goal = _viewModel?.CurrentGoal;", source);
        Assert.DoesNotContain("DataContext: SavingGoalVM goal", source);
    }

}
