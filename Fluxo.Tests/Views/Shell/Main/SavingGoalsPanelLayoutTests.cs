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
    public void SavingGoalTemplateExposesCompactMetrics()
    {
        var xaml = File.ReadAllText(SavingGoalsPanelXamlPath);

        Assert.Contains("Amount Left", xaml);
        Assert.Contains("Weekly Average", xaml);
        Assert.Contains("Estimated Deadline", xaml);
        Assert.DoesNotContain(">saved<", xaml);
        Assert.DoesNotContain("target", xaml);
    }

    [Fact]
    public void GoalActionButtonsLiveOutsideSavingGoalTemplate()
    {
        var xaml = File.ReadAllText(SavingGoalsPanelXamlPath);

        var templateStart = xaml.IndexOf("<DataTemplate x:Key=\"SavingGoalTemplate\">", StringComparison.Ordinal);
        var templateEnd = xaml.IndexOf("</DataTemplate>", templateStart, StringComparison.Ordinal);
        var editAction = xaml.IndexOf("OnEditSavingGoalClick", StringComparison.Ordinal);
        var fundsAction = xaml.IndexOf("OnAddGoalFundsClick", StringComparison.Ordinal);

        Assert.True(editAction > templateEnd);
        Assert.True(fundsAction > templateEnd);
    }

    [Fact]
    public void HeaderGoalActionsUseCurrentGoal()
    {
        var source = File.ReadAllText(SavingGoalsPanelCodeBehindPath);

        Assert.Contains("var goal = _viewModel?.CurrentGoal;", source);
        Assert.DoesNotContain("DataContext: SavingGoalVM goal", source);
    }

    [Fact]
    public void GoalNavigationDoesNotCrossfadeGoalContent()
    {
        var source = File.ReadAllText(SavingGoalsPanelCodeBehindPath);

        Assert.Contains("BeginAnimation(AnimatedProgressRatioProperty, progressAnimation)", source);
        Assert.DoesNotContain("CurrentGoalPresenter.BeginAnimation(OpacityProperty", source);
        Assert.DoesNotContain("IncomingGoalPresenter.BeginAnimation(OpacityProperty", source);
    }
}
