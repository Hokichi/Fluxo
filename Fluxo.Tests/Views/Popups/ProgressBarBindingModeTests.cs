using System.IO;
using Xunit;

namespace Fluxo.Tests.Views.Popups;

public sealed class ProgressBarBindingModeTests
{
    private static readonly string AnalyticsXamlPath = ResolveRepoPath("Fluxo", "Views", "Shell", "Main", "Pages", "Analytics.xaml");
    private static readonly string SpendingSourceDetailPopupXamlPath = ResolveRepoPath("Fluxo", "Views", "Popups", "SpendingSourceDetailPopup.xaml");

    [Fact]
    public void AnalyticsProgressBindings_AreOneWay()
    {
        var xaml = File.ReadAllText(AnalyticsXamlPath);

        var oneWayMarker = "Value=\"{Binding ProgressPercent, Mode=OneWay}\"";
        var oneWayUsageCount = xaml.Split(oneWayMarker).Length - 1;

        Assert.True(oneWayUsageCount >= 2, "Expected goal and top-tag progress bars to use OneWay bindings.");
    }

    [Fact]
    public void SpendingSourceTrendProgressBindings_AreOneWay()
    {
        var xaml = File.ReadAllText(SpendingSourceDetailPopupXamlPath);

        Assert.Contains("Value=\"{Binding IncomeAmount, Mode=OneWay}\"", xaml);
        Assert.Contains("Value=\"{Binding ExpenseAmount, Mode=OneWay}\"", xaml);
    }

    private static string ResolveRepoPath(params string[] relativeSegments)
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDirectory is not null)
        {
            var solutionPath = Path.Combine(currentDirectory.FullName, "Fluxo.sln");
            var solutionXPath = Path.Combine(currentDirectory.FullName, "Fluxo.slnx");
            if (File.Exists(solutionPath) || File.Exists(solutionXPath))
                return Path.Combine(currentDirectory.FullName, Path.Combine(relativeSegments));

            currentDirectory = currentDirectory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
