using System.Text.RegularExpressions;
using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Popups;

public sealed class RecurringDateInputControlTests
{
    [Fact]
    public void AddNewTransaction_RecurringDateInputsUseBoundedNumericUpDown()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "AddNewTransaction.xaml"));

        Assert.Equal(3, CountNumericUpDownsBoundTo(xaml, "RecurringDayText"));
        Assert.DoesNotContain("OnRecurringDatePasting", xaml);
        Assert.DoesNotContain("OnRecurringDatePreviewKeyDown", xaml);
        Assert.DoesNotContain("OnRecurringDatePreviewTextInput", xaml);
    }

    [Fact]
    public void AddFixedExpensePopup_RecurringDateInputUsesBoundedNumericUpDown()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "AddFixedExpensePopup.xaml"));

        Assert.Equal(1, CountNumericUpDownsBoundTo(xaml, "RecurringDateText"));
        Assert.DoesNotContain("RecurringDateTextBox", xaml);
        Assert.DoesNotContain("OnRecurringDatePasting", xaml);
        Assert.DoesNotContain("OnRecurringDatePreviewKeyDown", xaml);
        Assert.DoesNotContain("OnRecurringDatePreviewTextInput", xaml);
    }

    private static int CountNumericUpDownsBoundTo(string xaml, string propertyName)
    {
        var pattern = "<customControls:NumericUpDown\\b" +
                      $"(?=[^>]*\\bValue=\"{{Binding {propertyName},[^\"]*}}\")" +
                      "(?=[^>]*\\bStep=\"1\")" +
                      "(?=[^>]*\\bLowerLimit=\"0\")" +
                      "(?=[^>]*\\bUpperLimit=\"28\")" +
                      "[^>]*/>";

        return Regex.Matches(
            xaml,
            pattern,
            RegexOptions.Singleline).Count;
    }
}
