using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Popups;

public sealed class TransactionDetailModeLayoutTests
{
    [Fact]
    public void NamePanel_UsesIndependentExcludeAndTwoModes()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "TransactionDetailPopup.xaml"));
        Assert.Contains("IsChecked=\"{Binding IsExcludedFromBudget, Mode=TwoWay}\"", xaml);
        Assert.Contains("UncheckedIcon=\"{StaticResource CreditCardOff}\"", xaml);
        Assert.Equal(2, xaml.Split("GroupName=\"TransactionDetailMode\"", StringSplitOptions.None).Length - 1);
        Assert.Contains("UncheckedText=\"Regular\"", xaml);
        Assert.Contains("UncheckedText=\"Debt/IoU\"", xaml);
    }
}
