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
        Assert.Equal(3, Regex.Matches(xaml, "Visibility=\"\\{Binding ShowRecurringNoneInput, Converter=\\{StaticResource BoolToVisibilityConverter\\}\\}\"").Count);
        Assert.DoesNotContain("RecurringDayText", xaml);
        Assert.DoesNotContain("OnRecurringDatePasting", xaml);
        Assert.DoesNotContain("OnRecurringDatePreviewKeyDown", xaml);
        Assert.DoesNotContain("OnRecurringDatePreviewTextInput", xaml);
    }

    [Fact]
    public void AddFixedExpensePopup_RecurringTimeInputUsesBoundedNumericUpDown()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "AddFixedExpensePopup.xaml"));

        Assert.Equal(1, CountNumericUpDownsBoundTo(xaml, "RecurringTimeText", "0", "28"));
        Assert.DoesNotContain("RecurringDateText", xaml);
        Assert.DoesNotContain("RecurringDateTextBox", xaml);
        Assert.DoesNotContain("OnRecurringDatePasting", xaml);
        Assert.DoesNotContain("OnRecurringDatePreviewKeyDown", xaml);
        Assert.DoesNotContain("OnRecurringDatePreviewTextInput", xaml);
    }

    private static int CountNumericUpDownsBoundTo(string xaml, string propertyName, string lowerLimit, string upperLimit)
    {
        var pattern = "<customControls:NumericUpDown\\b" +
                      $"(?=[^>]*\\bValue=\"{{Binding {propertyName},[^\"]*}}\")" +
                      "(?=[^>]*\\bStep=\"1\")" +
                      $"(?=[^>]*\\bLowerLimit=\"{lowerLimit}\")" +
                      $"(?=[^>]*\\bUpperLimit=\"{upperLimit}\")" +
                      "[^>]*/>";

        return Regex.Matches(
            xaml,
            pattern,
            RegexOptions.Singleline).Count;
    }
}
