using System.Text.RegularExpressions;
using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Popups;

public sealed class RecurringDateInputControlTests
{
    [Fact]
    public void AddNewTransaction_RecurringTimeInputsSwitchByPeriod()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "AddNewTransaction.xaml"));

        Assert.Equal(3, CountNumericUpDownsBoundTo(xaml, "RecurringTimeText", "1", "28"));
        Assert.Equal(3, Regex.Matches(xaml, "ItemsSource=\"\\{Binding RecurringPeriods\\}\"").Count);
        Assert.Equal(3, Regex.Matches(xaml, "ItemsSource=\"\\{Binding WeekdayOptions\\}\"").Count);
        Assert.Equal(3, Regex.Matches(xaml, "SelectedValue=\"\\{Binding RecurringTimeText, Mode=TwoWay\\}\"").Count);
        Assert.Equal(3, Regex.Matches(xaml, "Visibility=\"\\{Binding ShowRecurringDayInput, Converter=\\{StaticResource BoolToVisibilityConverter\\}\\}\"").Count);
        Assert.DoesNotContain("RecurringDayText", xaml);
        Assert.DoesNotContain("OnRecurringDatePasting", xaml);
        Assert.DoesNotContain("OnRecurringDatePreviewKeyDown", xaml);
        Assert.DoesNotContain("OnRecurringDatePreviewTextInput", xaml);
    }

    [Fact]
    public void AddNewTransaction_DebtIouAndRecurringTogglesAreHorizontal()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "AddNewTransaction.xaml"));

        Assert.Equal(2, CountOccurrences(xaml, "<StackPanel Grid.Column=\"2\" Orientation=\"Horizontal\">"));
        Assert.Equal(2, CountOccurrences(xaml, "IsChecked=\"{Binding IsDebtIou, Mode=TwoWay}\""));
        Assert.Equal(2, CountOccurrences(xaml, "ToolTip=\"{Binding DebtIouTooltip}\""));
        Assert.Equal(2, CountOccurrences(xaml, "PreviewMouseLeftButtonDown=\"OnDebtIouModePreviewMouseLeftButtonDown\""));
        Assert.DoesNotContain("Margin=\"0,0,0,8\"", xaml);
    }

    [Fact]
    public void AddAccountPopup_MonthlyDueDateInputUsesBoundedNumericUpDown()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "AddAccountPopup.xaml"));

        Assert.Equal(1, CountNumericUpDownsBoundTo(xaml, "MonthlyDueDateText", "1", "28"));
        Assert.DoesNotContain("OnMonthlyDueDatePasting", xaml);
        Assert.DoesNotContain("OnMonthlyDueDatePreviewKeyDown", xaml);
        Assert.DoesNotContain("OnMonthlyDueDatePreviewTextInput", xaml);
    }

    [Fact]
    public void AddAccountPopup_MaximumSpendingShowsForCheckingCashAndCredit()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "AddAccountPopup.xaml"));

        Assert.Contains("Binding=\"{Binding SelectedAccountType}\" Value=\"{x:Static enums:AccountType.Checking}\"", xaml);
        Assert.Contains("Binding=\"{Binding SelectedAccountType}\" Value=\"{x:Static enums:AccountType.Cash}\"", xaml);
        Assert.Contains("Binding=\"{Binding SelectedAccountType}\" Value=\"{x:Static enums:AccountType.Credit}\"", xaml);
    }

    private static int CountNumericUpDownsBoundTo(string xaml, string propertyName, string lowerLimit, string upperLimit)
    {
        var valueBindingCount = Regex.Matches(
            xaml,
            $"(?<!Selected)Value=\"\\{{Binding {propertyName},").Count;
        var lowerLimitCount = CountOccurrences(xaml, $"LowerLimit=\"{lowerLimit}\"");
        var upperLimitCount = CountOccurrences(xaml, $"UpperLimit=\"{upperLimit}\"");
        var stepCount = CountOccurrences(xaml, "Step=\"1\"");

        return Math.Min(Math.Min(valueBindingCount, lowerLimitCount), Math.Min(upperLimitCount, stepCount));
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var startIndex = 0;
        while (true)
        {
            var index = text.IndexOf(value, startIndex, StringComparison.Ordinal);
            if (index < 0)
                return count;

            count++;
            startIndex = index + value.Length;
        }
    }
}
