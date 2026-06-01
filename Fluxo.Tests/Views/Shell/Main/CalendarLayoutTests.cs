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

        Assert.Contains("x:Class=\"Fluxo.Views.Shell.Main.Calendar\"", xaml);
        Assert.Contains("<ColumnDefinition Width=\"350", xaml);
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
                    "Calendar.xaml"));
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate Fluxo repository root.");
    }
}
