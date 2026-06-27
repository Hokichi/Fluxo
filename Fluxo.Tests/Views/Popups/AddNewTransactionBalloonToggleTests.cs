using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Popups;

public sealed class AddNewTransactionBalloonToggleTests
{
    [Fact]
    public void TransactionModes_UseFiveStateBalloonToggles()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "AddNewTransaction.xaml"));
        var codeBehind = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "AddNewTransaction.xaml.cs"));

        Assert.Equal(2, xaml.Split("ToolTip=\"Transaction mode\"", StringSplitOptions.None).Length - 1);
        Assert.Equal(2, xaml.Split("ButtonText=\"Recurring\"", StringSplitOptions.None).Length - 1);
        Assert.Equal(2, xaml.Split("ButtonText=\"Installments\"", StringSplitOptions.None).Length - 1);
        Assert.Equal(2, xaml.Split("ButtonText=\"Exclude\"", StringSplitOptions.None).Length - 1);
        Assert.Equal(2, xaml.Split("ButtonText=\"ExcludedIoU\"", StringSplitOptions.None).Length - 1);
        Assert.Equal(2, xaml.Split("ButtonIcon=\"{StaticResource CreditCardOff}\"", StringSplitOptions.None).Length - 1);
        Assert.Equal(2, xaml.Split("ButtonIcon=\"{StaticResource CreditCardXRegular}\"", StringSplitOptions.None).Length - 1);
        Assert.Contains("OnChecked=\"{Binding HandleExcludeModeClickCommand}\"", xaml);
        Assert.Contains("OnChecked=\"{Binding HandleExcludedIoUModeClickCommand}\"", xaml);
        Assert.DoesNotContain("OnRecurringModePreviewMouseLeftButtonDown", xaml + codeBehind);
        Assert.DoesNotContain("OnInstallmentsModePreviewMouseLeftButtonDown", xaml + codeBehind);
        Assert.DoesNotContain("OnIoUModePreviewMouseLeftButtonDown", xaml + codeBehind);
    }
}
