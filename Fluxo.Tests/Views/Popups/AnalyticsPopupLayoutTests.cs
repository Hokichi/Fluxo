using System.IO;
using Xunit;

namespace Fluxo.Tests.Views.Popups;

public sealed class AnalyticsPopupLayoutTests
{
    private static readonly string AnalyticsPopupXamlPath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "..",
        "Fluxo",
        "Views",
        "Popups",
        "AnalyticsPopup.xaml"));

    [Fact]
    public void TrendModeUsesSegmentedToggleButtons_InsteadOfComboBox()
    {
        var xaml = File.ReadAllText(AnalyticsPopupXamlPath);

        Assert.Contains("Command=\"{Binding SetTrendModeCommand}\"", xaml);
        Assert.Contains("Style=\"{StaticResource MainContentViewToggleButtonStyle}\"", xaml);
        Assert.DoesNotContain("<ComboBox", xaml);
    }

    [Fact]
    public void ChartsShowNoDataFound_WhenTheirDataIsUnavailable()
    {
        var xaml = File.ReadAllText(AnalyticsPopupXamlPath);

        Assert.Contains("Visibility=\"{Binding HasTrendData, Converter={StaticResource BoolToVisibilityInvertedConverter}}\"", xaml);
        Assert.Contains("Visibility=\"{Binding HasRatioData, Converter={StaticResource BoolToVisibilityInvertedConverter}}\"", xaml);
        Assert.Contains("Visibility=\"{Binding HasTagData, Converter={StaticResource BoolToVisibilityInvertedConverter}}\"", xaml);
        Assert.Contains("Text=\"No Data Found\"", xaml);
    }

    [Fact]
    public void DateRangeWarningMessage_IsBoundInHeader()
    {
        var xaml = File.ReadAllText(AnalyticsPopupXamlPath);

        Assert.Contains("Text=\"{Binding DateRangeWarningMessage}\"", xaml);
        Assert.Contains("Visibility=\"{Binding HasDateRangeWarning, Converter={StaticResource BoolToVisibilityConverter}}\"", xaml);
    }

    [Fact]
    public void AnalyticsUsesNativeWpfChartsAndSharedProgressBars()
    {
        var xaml = File.ReadAllText(AnalyticsPopupXamlPath);

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
}
