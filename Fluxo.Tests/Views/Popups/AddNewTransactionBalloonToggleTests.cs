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
        Assert.Contains("Visibility=\"{Binding ShowCategoryOrRepaymentField, Mode=OneWay, Converter={StaticResource BoolToVisibilityConverter}}\"", xaml);
        Assert.DoesNotContain("ShowDisabledCategoryField", xaml);
    }

    [Fact]
    public void TransactionModes_UseIndependentExclusionAndFourRadioButtons()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "AddNewTransaction.xaml"));
        var codeBehind = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "AddNewTransaction.xaml.cs"));

        Assert.Contains("IsChecked=\"{Binding IsBudgetExcluded, Mode=TwoWay}\"", xaml);
        Assert.Contains("UncheckedIcon=\"{StaticResource CreditCardOff}\"", xaml);
        Assert.Equal(4, xaml.Split("GroupName=\"AddTransactionMode\"", StringSplitOptions.None).Length - 1);
        Assert.Contains("UncheckedText=\"Regular\"", xaml);
        Assert.Contains("UncheckedText=\"Recurring\"", xaml);
        Assert.Contains("UncheckedText=\"Installment\"", xaml);
        Assert.Contains("UncheckedText=\"Debt/IoU\"", xaml);
        Assert.Contains("SelectedDate=\"{Binding StartDate, Mode=TwoWay}\"", xaml);
        Assert.Contains("Visibility=\"{Binding ShowTransactionModes, Mode=OneWay, Converter={StaticResource BoolToVisibilityConverter}}\"", xaml);
        Assert.Contains("Visibility=\"{Binding ShowCategoryField, Mode=OneWay, Converter={StaticResource BoolToVisibilityConverter}}\"", xaml);
        Assert.Contains("DataTrigger Binding=\"{Binding ShouldExpandAccountField, Mode=OneWay}\" Value=\"True\"", xaml);
        Assert.DoesNotContain("OnRecurringModePreviewMouseLeftButtonDown", xaml + codeBehind);
        Assert.DoesNotContain("OnInstallmentsModePreviewMouseLeftButtonDown", xaml + codeBehind);
        Assert.DoesNotContain("OnIoUModePreviewMouseLeftButtonDown", xaml + codeBehind);
    }

    [Fact]
    public void History_StartsOpenAndLoadsWithPopup()
    {
        var viewModel = File.ReadAllText(RepositoryPaths.File("Fluxo", "ViewModels", "Popups", "AddNewTransactionVM.cs"));
        var codeBehind = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "AddNewTransaction.xaml.cs"));

        Assert.Contains("[ObservableProperty] private bool _isHistoryOpen = true;", viewModel);
        Assert.Contains("if (_viewModel.IsHistoryOpen)", codeBehind);
        Assert.Contains("await _viewModel.LoadHistoryAsync();", codeBehind);
    }
}
