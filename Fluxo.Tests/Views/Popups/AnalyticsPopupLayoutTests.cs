using System.IO;
using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Popups;

public sealed class AnalyticsPopupLayoutTests
{
    private static readonly string AnalyticsXamlPath = RepositoryPaths.File(
        "Fluxo",
        "Views",
        "Shell",
        "Main",
        "Analytics.xaml");

    [Fact]
    public void TrendModeUsesSegmentedToggleButtons_InsteadOfComboBox()
    {
        var xaml = File.ReadAllText(AnalyticsXamlPath);

        Assert.Contains("Command=\"{Binding SetTrendModeCommand}\"", xaml);
        Assert.Contains("Style=\"{StaticResource MainContentViewToggleButtonStyle}\"", xaml);
        Assert.DoesNotContain("<ComboBox", xaml);
    }

    [Fact]
    public void ChartsShowNoDataFound_WhenTheirDataIsUnavailable()
    {
        var xaml = File.ReadAllText(AnalyticsXamlPath);

        Assert.Contains("Visibility=\"{Binding HasTrendData, Converter={StaticResource BoolToVisibilityInvertedConverter}}\"", xaml);
        Assert.Contains("Visibility=\"{Binding HasRatioData, Converter={StaticResource BoolToVisibilityInvertedConverter}}\"", xaml);
        Assert.Contains("Visibility=\"{Binding HasTagData, Converter={StaticResource BoolToVisibilityInvertedConverter}}\"", xaml);
        Assert.Contains("x:Key=\"AnalyticsNoDataTextStyle\"", xaml);
        Assert.Contains("<Setter Property=\"Text\" Value=\"No Data Found\" />", xaml);
    }

    [Fact]
    public void AnalyticsUsesNativeWpfChartsAndSharedProgressBars()
    {
        var xaml = File.ReadAllText(AnalyticsXamlPath);

        Assert.Contains("<components:AnalyticsBarChart", xaml);
        Assert.Contains("ItemsSource=\"{Binding TrendBarItems}\"", xaml);
        Assert.DoesNotContain("lvc:", xaml);
        Assert.Contains("ProgressToArcGeometryConverter", xaml);
        Assert.Contains("ItemsSource=\"{Binding TopSpendingTagItems}\"", xaml);
        Assert.Contains("Style=\"{StaticResource AnalyticsCardProgressBarStyle}\"", xaml);
        Assert.Contains("Background=\"{StaticResource Brush.Background.Surface}\"", xaml);

        var sharedProgressBarStyleMarker = "Style=\"{StaticResource AnalyticsCardProgressBarStyle}\"";
        var sharedProgressBarStyleUsageCount = xaml.Split(sharedProgressBarStyleMarker).Length - 1;
        Assert.True(sharedProgressBarStyleUsageCount >= 2, "Expected top tags and goals to share the same progress bar style.");
    }

    [Fact]
    public void RatioCardStacksLegendBelowResponsiveDonut()
    {
        var xaml = File.ReadAllText(AnalyticsXamlPath);

        Assert.Contains("<RowDefinition Height=\"*\" />", xaml);
        Assert.Contains("<RowDefinition Height=\"Auto\" />", xaml);
        Assert.Contains("<Viewbox", xaml);
        Assert.Contains("Stretch=\"Uniform\"", xaml);
        Assert.DoesNotContain("Width=\"180\"", xaml);
        Assert.DoesNotContain("Height=\"180\"", xaml);
        Assert.DoesNotContain("<ColumnDefinition Width=\"14\" />", xaml);
    }
}
