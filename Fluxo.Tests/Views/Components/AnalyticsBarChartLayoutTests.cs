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
    public void ChartBarsAreResponsive_AndNotHardcoded()
    {
        var xaml = File.ReadAllText(AnalyticsBarChartXamlPath);

        Assert.DoesNotContain("Width=\"620\"", xaml);
        Assert.Contains("RowDefinition Height=\"*\"", xaml);
        Assert.Contains("ScaleTransform ScaleY=\"{Binding BarHeightRatio}\"", xaml);
        Assert.Contains("RenderTransformOrigin=\"0.5,1\"", xaml);
        Assert.Contains("HorizontalAlignment=\"Stretch\"", xaml);
        Assert.DoesNotContain("Width=\"56\"", xaml);
        Assert.Contains("Binding=\"{Binding HideValueText}\"", xaml);
        Assert.Contains("Binding=\"{Binding RotateLabelVertical}\"", xaml);
        Assert.Contains("Binding=\"{Binding IsExpenseMode}\"", xaml);
        Assert.Contains("Binding=\"{Binding IsIncomeMode}\"", xaml);
        Assert.Contains("Binding=\"{Binding HasSecondaryBar}\"", xaml);
        Assert.Contains("ScaleTransform ScaleY=\"{Binding SecondaryBarHeightRatio}\"", xaml);
        Assert.Contains("Binding=\"{Binding IsSecondaryIncomeMode}\"", xaml);
        Assert.Contains("Brush.Danger", xaml);
        Assert.Contains("Brush.Mint", xaml);
    }
}
