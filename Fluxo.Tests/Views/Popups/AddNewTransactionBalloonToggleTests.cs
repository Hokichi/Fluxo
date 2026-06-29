using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Popups;

public sealed class AddNewTransactionBalloonToggleTests
{
    [Fact]
    public void RepaymentTab_UsesRepaymentStateAndCreditAccountSelector()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "AddNewTransaction.xaml"));

        Assert.Contains("Content=\"Repayment\"", xaml);
        Assert.Contains("IsSelected=\"{Binding IsRepayment, Mode=TwoWay}\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding RepaymentAccounts}\"", xaml);
        Assert.Contains("SelectedItem=\"{Binding SelectedRepaymentAccount, Mode=TwoWay}\"", xaml);
        Assert.Contains("IsEnabled=\"{Binding CanChangeRepaymentAccount}\"", xaml);
        Assert.Contains("Visibility=\"{Binding ShowDisabledCategoryField, Converter={StaticResource BoolToVisibilityConverter}}\"", xaml);
    }

    [Fact]
    public void TransactionModes_UseFiveStateBalloonToggles()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "AddNewTransaction.xaml"));
        var codeBehind = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "AddNewTransaction.xaml.cs"));

        Assert.Equal(1, xaml.Split("ToolTip=\"Transaction mode\"", StringSplitOptions.None).Length - 1);
        Assert.Contains("ButtonText=\"Recurring\"", xaml);
        Assert.Contains("ButtonText=\"Installments\"", xaml);
        Assert.Equal(1, xaml.Split("ButtonText=\"Exclude\"", StringSplitOptions.None).Length - 1);
        Assert.Contains("ButtonText=\"Exclude from budget\"", xaml);
        Assert.Equal(1, xaml.Split("ButtonText=\"Set as Debt and Exclude\"", StringSplitOptions.None).Length - 1);
        Assert.Equal(1, xaml.Split("ButtonText=\"Set as debt and exclude\"", StringSplitOptions.None).Length - 1);
        Assert.Equal(2, xaml.Split("ButtonIcon=\"{StaticResource CreditCardOff}\"", StringSplitOptions.None).Length - 1);
        Assert.Equal(2, xaml.Split("ButtonIcon=\"{StaticResource CreditCardXRegular}\"", StringSplitOptions.None).Length - 1);
        Assert.Contains("OnChecked=\"{Binding HandleExcludeModeClickCommand}\"", xaml);
        Assert.Contains("OnChecked=\"{Binding HandleExcludedIoUModeClickCommand}\"", xaml);
        Assert.DoesNotContain("OnRecurringModePreviewMouseLeftButtonDown", xaml + codeBehind);
        Assert.DoesNotContain("OnInstallmentsModePreviewMouseLeftButtonDown", xaml + codeBehind);
        Assert.DoesNotContain("OnIoUModePreviewMouseLeftButtonDown", xaml + codeBehind);
    }
}
