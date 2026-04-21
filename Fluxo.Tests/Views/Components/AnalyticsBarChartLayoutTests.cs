using System.IO;
using Xunit;

namespace Fluxo.Tests.Views.Components;

public sealed class AnalyticsBarChartLayoutTests
{
    private static readonly string AnalyticsBarChartXamlPath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "..",
        "Fluxo",
        "Views",
        "Components",
        "AnalyticsBarChart.xaml"));

    [Fact]
    public void ChartWidthIsFixed_AndBarsAreNotHardcoded()
    {
        var xaml = File.ReadAllText(AnalyticsBarChartXamlPath);

        Assert.Contains("Width=\"620\"", xaml);
        Assert.Contains("HorizontalAlignment=\"Stretch\"", xaml);
        Assert.DoesNotContain("Width=\"56\"", xaml);
        Assert.Contains("Binding=\"{Binding HideValueText}\"", xaml);
        Assert.Contains("Binding=\"{Binding RotateLabelVertical}\"", xaml);
        Assert.Contains("Binding=\"{Binding IsExpenseMode}\"", xaml);
        Assert.Contains("Binding=\"{Binding IsIncomeMode}\"", xaml);
        Assert.Contains("Brush.Danger", xaml);
        Assert.Contains("Brush.Mint", xaml);
    }
}
