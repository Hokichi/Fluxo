using System.IO;
using Xunit;

namespace Fluxo.Tests.Views.Popups;

public sealed class ProgressBarBindingModeTests
{
    private static readonly string AnalyticsXamlPath = ResolveViewPath("Shell", "Main", "Analytics.xaml");
    private static readonly string SpendingSourceDetailPopupXamlPath = ResolveViewPath("Popups", "SpendingSourceDetailPopup.xaml");

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

    private static string ResolveViewPath(params string[] relativeSegments)
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "Fluxo",
            "Views",
            Path.Combine(relativeSegments)));
    }
}
