using System.IO;
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
        Assert.Contains("Command=\"{Binding SelectDateCommand}\"", xaml);
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
