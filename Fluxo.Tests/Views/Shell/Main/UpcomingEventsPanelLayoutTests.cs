using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Shell.Main;

public class UpcomingEventsPanelLayoutTests
{
    private static readonly string PanelPath = RepositoryPaths.File(
        "Fluxo",
        "Views",
        "Shell",
        "Main",
        "Sections",
        "UpcomingEventsPanel.xaml");

    [Fact]
    public void UpcomingEventsPanel_HasExpectedHeaderAndNoWindowSubtitle()
    {
        var xaml = File.ReadAllText(PanelPath);

        Assert.Contains("Text=\"Upcoming Events\"", xaml);
        Assert.DoesNotContain("Next 14 days", xaml);
    }

    [Fact]
    public void UpcomingEventsPanel_UsesScrollableEventsList()
    {
        var xaml = File.ReadAllText(PanelPath);

        Assert.Contains("x:Name=\"UpcomingEventsScrollViewer\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding Events}\"", xaml);
        Assert.Contains("VerticalScrollBarVisibility=\"Auto\"", xaml);
    }

    [Fact]
    public void UpcomingEventsPanel_ItemTemplateShowsDateTitleAndAmount()
    {
        var xaml = File.ReadAllText(PanelPath);

        Assert.Contains("Text=\"{Binding MonthText}\"", xaml);
        Assert.Contains("Text=\"{Binding DayText}\"", xaml);
        Assert.Contains("Text=\"{Binding Title}\"", xaml);
        Assert.Contains("Text=\"{Binding AmountText}\"", xaml);
    }

    [Fact]
    public void UpcomingEventsPanel_ItemTemplateShowsEventTypeBadgeOnRightSide()
    {
        var xaml = File.ReadAllText(PanelPath);

        Assert.Contains("x:Name=\"UpcomingEventTypeBadge\"", xaml);
        Assert.Contains("Text=\"{Binding EventTypeText}\"", xaml);
        Assert.Contains("<ColumnDefinition Width=\"Auto\" />", xaml);
    }

    [Fact]
    public void UpcomingEventsPanel_ItemTemplateHasHoverEffect()
    {
        var xaml = File.ReadAllText(PanelPath);

        Assert.Contains("x:Name=\"UpcomingEventItemSurface\"", xaml);
        Assert.Contains("SourceName=\"UpcomingEventItemSurface\" Property=\"IsMouseOver\" Value=\"True\"", xaml);
        Assert.Contains("TargetName=\"UpcomingEventItemSurface\"", xaml);
    }
}
