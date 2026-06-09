using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Fluxo.Tests.Views.Shell.Main;

public sealed class CalendarLayoutTests
{
    [Fact]
    public void CalendarLayout_UsesCompactCalendarColumnAndLargeDataGrid()
    {
        var xaml = LoadCalendarXaml();

        Assert.Contains("x:Class=\"Fluxo.Views.Shell.Main.Pages.Calendar\"", xaml);
        Assert.Contains("<ColumnDefinition Width=\"Auto", xaml);
        Assert.Contains("<ColumnDefinition Width=\"*\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding VisibleWeeks}\"", xaml);
        Assert.Contains("Command=\"{Binding DataContext.SelectDateCommand, RelativeSource={RelativeSource AncestorType=UserControl}}\"", xaml);
    }

    [Fact]
    public void CalendarLayout_HasRequiredSummaryAndListCards()
    {
        var xaml = LoadCalendarXaml();

        Assert.Contains("Text=\"Total Spent\"", xaml);
        Assert.Contains("Text=\"Total Earned\"", xaml);
        Assert.Contains("Text=\"Goals Due\"", xaml);
        Assert.Contains("Text=\"Payment Due\"", xaml);
        Assert.Contains("Text=\"Expenses\"", xaml);
        Assert.Contains("Text=\"Incomes\"", xaml);
        Assert.Contains("Text=\"Goal Deadlines\"", xaml);
        Assert.Contains("Text=\"Recurring Transactions\"", xaml);
    }

    [Fact]
    public void CalendarLayout_DateCellsBindOnlyToDayNumber()
    {
        var xaml = LoadCalendarXaml();

        Assert.Contains("Text=\"{Binding DayNumber}\"", xaml);
        Assert.DoesNotContain("Calendar cell amount", xaml);
        Assert.DoesNotContain("Expense count", xaml);
    }

    [Fact]
    public void CalendarLayout_DayButtonsKeepDayItemDataContextForStyleTriggers()
    {
        var xaml = LoadCalendarXaml();

        Assert.Contains("Command=\"{Binding DataContext.SelectDateCommand, RelativeSource={RelativeSource AncestorType=UserControl}}\"", xaml);
        Assert.Contains("CommandParameter=\"{Binding}\"", xaml);
        Assert.DoesNotContain("DataContext=\"{Binding DataContext, RelativeSource={RelativeSource AncestorType=UserControl}}\"", xaml);
        Assert.DoesNotContain("CommandParameter=\"{Binding Tag, RelativeSource={RelativeSource AncestorType=Grid}}\"", xaml);
    }

    [Fact]
    public void CalendarLayout_DayTextUsesFixedSelectionForegroundBrushes()
    {
        var xaml = LoadCalendarXaml();
        var dayTemplate = ExtractSection(xaml, "x:Key=\"CalendarDayTemplate\"", "x:Key=\"CalendarWeekTemplate\"");

        Assert.Contains("Property=\"Foreground\" Value=\"{StaticResource Brush.Text.Primary}\"", dayTemplate);
        Assert.Contains("Binding=\"{Binding IsSelected}\" Value=\"True\"", dayTemplate);
        Assert.Contains("Brush.Text.Primary.Dark", dayTemplate);
        Assert.DoesNotContain("ForegroundForBackgroundBrushConverter", dayTemplate);
    }

    [Fact]
    public void CalendarLayout_SelectedDayHoverUsesMutedMintBackground()
    {
        var xaml = LoadCalendarXaml();
        var dayButtonStyle = ExtractSection(xaml, "x:Key=\"CalendarDayButtonStyle\"", "x:Key=\"CalendarDayTemplate\"");

        Assert.Contains("Binding=\"{Binding IsSelected}\" Value=\"True\"", dayButtonStyle);
        Assert.Contains("Property=\"IsMouseOver\" Value=\"True\"", dayButtonStyle);
        Assert.Contains("Property=\"Background\" Value=\"{StaticResource Brush.Mint.Muted}\"", dayButtonStyle);
    }

    [Fact]
    public void CalendarLayout_CurrentDayBorderUsesMintAndMutedMintOnHover()
    {
        var xaml = LoadCalendarXaml();
        var dayButtonStyle = ExtractSection(xaml, "x:Key=\"CalendarDayButtonStyle\"", "x:Key=\"CalendarDayTemplate\"");

        Assert.Contains("Binding=\"{Binding IsCurrentDay}\" Value=\"True\"", dayButtonStyle);
        Assert.Contains("Property=\"BorderBrush\" Value=\"{StaticResource Brush.Mint}\"", dayButtonStyle);
        Assert.Contains("Property=\"BorderBrush\" Value=\"{StaticResource Brush.Mint.Muted}\"", dayButtonStyle);
    }

    [Fact]
    public void CalendarLayout_MonthHeaderContainsBalloonMonthNavigationButtons()
    {
        var xaml = LoadCalendarXaml();
        var document = XDocument.Parse(xaml);
        var presentation = document.Root!.Name.Namespace;
        var calendarRoot = document.Root
            .Elements(presentation + "Grid")
            .Single(element => (string?)element.Attribute("Margin") == "12");
        var leftColumn = calendarRoot
            .Elements(presentation + "Grid")
            .Single(element => element.Attribute("Grid.Column") is null);
        var monthHeader = leftColumn
            .Elements(presentation + "Border")
            .Single(element => element.Attribute("Grid.Row") is null);

        AssertTextExists(monthHeader, "{Binding VisibleMonthLabel}");
        Assert.Contains(monthHeader.Descendants().Attributes("ButtonIcon"), attribute => attribute.Value == "{StaticResource AngleUp}");
        Assert.Contains(monthHeader.Descendants().Attributes("ButtonIcon"), attribute => attribute.Value == "{StaticResource AngleDown}");
        Assert.Contains(monthHeader.Descendants().Attributes("Command"), attribute => attribute.Value.Contains("NavigateToPreviousMonthCommand", StringComparison.Ordinal));
        Assert.Contains(monthHeader.Descendants().Attributes("Command"), attribute => attribute.Value.Contains("NavigateToNextMonthCommand", StringComparison.Ordinal));
    }

    [Fact]
    public void CalendarLayout_DoesNotSetCanContentScroll()
    {
        var xaml = LoadCalendarXaml();

        Assert.DoesNotContain("CanContentScroll", xaml);
    }

    [Fact]
    public void CalendarLayout_CentersWeekHeadersOverDateCells()
    {
        var xaml = LoadCalendarXaml();

        Assert.Contains("x:Key=\"CalendarWeekHeaderLabelStyle\"", xaml);
        Assert.Contains("<Setter Property=\"TextAlignment\" Value=\"Center\" />", xaml);
        Assert.Contains("Style=\"{StaticResource CalendarWeekHeaderLabelStyle}\" Text=\"SUN\"", xaml);
        Assert.Contains("Style=\"{StaticResource CalendarWeekHeaderLabelStyle}\" Text=\"SAT\"", xaml);
    }

    [Fact]
    public void CalendarLayout_UsesStackedSummaryCardsWhenWindowIsMaximized()
    {
        var xaml = LoadCalendarXaml();

        Assert.Contains("x:Key=\"CalendarSummaryMaximizedVisibilityStyle\"", xaml);
        Assert.Contains("Binding=\"{Binding IsWindowLayoutMaximized, RelativeSource={RelativeSource AncestorType=Window}}\" Value=\"True\"", xaml);
        Assert.Contains("x:Key=\"CalendarSummaryInlineValueTextStyle\"", xaml);
        Assert.Contains("<Setter Property=\"Margin\" Value=\"12,0,0,0\" />", xaml);
        Assert.Contains("<Setter Property=\"HorizontalAlignment\" Value=\"Right\" />", xaml);
    }

    [Fact]
    public void CalendarLayout_ResizesCalendarCardWithWindowLayoutState()
    {
        var xaml = LoadCalendarXaml();

        Assert.Contains("x:Key=\"CalendarColumnLayoutStyle\"", xaml);
        Assert.Contains("x:Key=\"CalendarGridCardStyle\"", xaml);
        Assert.Contains("<Setter Property=\"Width\" Value=\"480\" />", xaml);
        Assert.Contains("<Setter Property=\"Height\" Value=\"480\" />", xaml);
        Assert.Contains("<Setter Property=\"Width\" Value=\"570\" />", xaml);
        Assert.Contains("<Setter Property=\"Height\" Value=\"570\" />", xaml);
        Assert.Contains("Binding=\"{Binding IsWindowLayoutMaximized, RelativeSource={RelativeSource AncestorType=Window}}\" Value=\"True\"", xaml);
        Assert.Contains("Style=\"{StaticResource CalendarGridCardStyle}\"", xaml);
        Assert.Contains("Style=\"{StaticResource CalendarColumnLayoutStyle}\"", xaml);
    }

    [Fact]
    public void CalendarLayout_StretchesWeekRowsAndDateButtonsVertically()
    {
        var xaml = LoadCalendarXaml();

        Assert.Contains("<UniformGrid Rows=\"6\" />", xaml);
        Assert.Contains("<UniformGrid Columns=\"7\" Rows=\"1\" />", xaml);
        Assert.Contains("<Setter Property=\"VerticalAlignment\" Value=\"Stretch\" />", xaml);
        Assert.Contains("VerticalAlignment=\"Stretch\"", xaml);
    }

    [Fact]
    public void CalendarLayout_KeepsRestoredSummaryGridFullWidth()
    {
        var xaml = LoadCalendarXaml();

        Assert.Contains("x:Key=\"CalendarSummaryRestoredVisibilityStyle\"", xaml);
        Assert.Contains("<ColumnDefinition Width=\"*\" />", xaml);
        Assert.Contains("HorizontalAlignment=\"Stretch\"", xaml);
    }

    [Fact]
    public void CalendarLayout_PlacesSummaryCardsUnderCalendarInLeftColumn()
    {
        var xaml = LoadCalendarXaml();
        var document = XDocument.Parse(xaml);
        var presentation = document.Root!.Name.Namespace;
        var layoutGrid = document.Root
            .Elements(presentation + "Grid")
            .Single(element => (string?)element.Attribute("Margin") == "12");
        var leftColumn = layoutGrid
            .Elements(presentation + "Grid")
            .Single(element => element.Attribute("Grid.Column") is null);
        var rightColumn = layoutGrid
            .Elements(presentation + "Grid")
            .Single(element => (string?)element.Attribute("Grid.Column") == "2");

        AssertTextExists(leftColumn, "Total Spent");
        AssertTextExists(leftColumn, "Total Earned");
        AssertTextExists(leftColumn, "Goals Due");
        AssertTextExists(leftColumn, "Payment Due");

        AssertTextDoesNotExist(rightColumn, "Total Spent");
        AssertTextDoesNotExist(rightColumn, "Total Earned");
        AssertTextDoesNotExist(rightColumn, "Goals Due");
        AssertTextDoesNotExist(rightColumn, "Payment Due");

        AssertTextExists(rightColumn, "Expenses");
        AssertTextExists(rightColumn, "Incomes");
        AssertTextExists(rightColumn, "Goal Deadlines");
        AssertTextExists(rightColumn, "Recurring Transactions");
    }

    private static void AssertTextExists(XElement element, string text)
    {
        Assert.Contains(
            element.Descendants().Attributes("Text"),
            attribute => attribute.Value == text);
    }

    private static void AssertTextDoesNotExist(XElement element, string text)
    {
        Assert.DoesNotContain(
            element.Descendants().Attributes("Text"),
            attribute => attribute.Value == text);
    }

    private static string ExtractSection(string content, string startMarker, string endMarker)
    {
        var start = content.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find start marker '{startMarker}'.");

        var endSearchStart = start + startMarker.Length;
        var end = content.IndexOf(endMarker, endSearchStart, StringComparison.Ordinal);
        Assert.True(end >= 0, $"Could not find end marker '{endMarker}'.");

        return content[start..(end + endMarker.Length)];
    }

    private static string LoadCalendarXaml()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDirectory is not null)
        {
            if (File.Exists(Path.Combine(currentDirectory.FullName, "Fluxo.slnx")))
            {
                return File.ReadAllText(Path.Combine(
                    currentDirectory.FullName,
                    "Fluxo",
                    "Views",
                    "Shell",
                    "Main",
                    "Pages",
                    "Calendar.xaml"));
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate Fluxo repository root.");
    }
}
