using Fluxo.Core.Enums;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Shell.Main;
using Xunit;

namespace Fluxo.Tests.ViewModels.Shell.Main;

public class MainVMSpendingAmountGateTests
{
    [Fact]
    public void ShouldLockDashboardForSpendingAmount_WhenNoSources_ReturnsTrue()
    {
        var isLocked = DashboardVM.ShouldLockDashboardForSpendingAmount([], []);

        Assert.True(isLocked);
    }

    [Fact]
    public void ShouldLockDashboardForSpendingAmount_WhenEnabledNonCreditSourcesHaveNoPositiveBalance_ReturnsFalse()
    {
        var sources = new[]
        {
            new AccountVM { AccountType = AccountType.Cash, Balance = 0m, IsEnabled = true },
            new AccountVM { AccountType = AccountType.Checking, Balance = -10m, IsEnabled = true },
            new AccountVM { AccountType = AccountType.Saving, Balance = 0m, IsEnabled = true }
        };

        var isLocked = DashboardVM.ShouldLockDashboardForSpendingAmount(sources, []);

        Assert.False(isLocked);
    }

    [Fact]
    public void ShouldLockDashboardForSpendingAmount_WhenAnyNonCreditSourceHasPositiveBalance_ReturnsFalse()
    {
        var sources = new[]
        {
            new AccountVM { AccountType = AccountType.Cash, Balance = 0m, IsEnabled = true },
            new AccountVM { AccountType = AccountType.Checking, Balance = 25m, IsEnabled = true }
        };

        var isLocked = DashboardVM.ShouldLockDashboardForSpendingAmount(sources, []);

        Assert.False(isLocked);
    }

    [Fact]
    public void ShouldLockDashboardForSpendingAmount_WhenEnabledCreditHasNoPositiveLimit_ReturnsFalse()
    {
        var sources = new[]
        {
            new AccountVM { AccountType = AccountType.Credit, AccountLimit = 0m, IsEnabled = true }
        };

        var isLocked = DashboardVM.ShouldLockDashboardForSpendingAmount(sources, []);

        Assert.False(isLocked);
    }

    [Fact]
    public void ShouldLockDashboardForSpendingAmount_WhenAnyCreditHasPositiveLimit_ReturnsFalse()
    {
        var sources = new[]
        {
            new AccountVM { AccountType = AccountType.Credit, AccountLimit = 500m, IsEnabled = true }
        };

        var isLocked = DashboardVM.ShouldLockDashboardForSpendingAmount(sources, []);

        Assert.False(isLocked);
    }

    [Fact]
    public void ShouldLockDashboardForSpendingAmount_WhenNoSourcesButHasExpenseLog_ReturnsTrue()
    {
        var logs = new[]
        {
            new ExpenseLogVM { IsForDeletion = false, Amount = 10m }
        };

        var isLocked = DashboardVM.ShouldLockDashboardForSpendingAmount([], logs);

        Assert.True(isLocked);
    }

    [Fact]
    public void ShouldLockDashboardForSpendingAmount_WhenNonCreditSourcesHaveNoPositiveBalanceButHasExpenseLog_ReturnsFalse()
    {
        var sources = new[]
        {
            new AccountVM { AccountType = AccountType.Cash, Balance = 0m, IsEnabled = true }
        };
        var logs = new[]
        {
            new ExpenseLogVM { IsForDeletion = false, Amount = 50m }
        };

        var isLocked = DashboardVM.ShouldLockDashboardForSpendingAmount(sources, logs);

        Assert.False(isLocked);
    }

    [Fact]
    public void ShouldLockDashboardForSpendingAmount_WhenEnabledSourceHasExpenseLogMarkedForDeletion_ReturnsFalse()
    {
        var sources = new[]
        {
            new AccountVM { AccountType = AccountType.Cash, Balance = 0m, IsEnabled = true }
        };
        var logs = new[]
        {
            new ExpenseLogVM { IsForDeletion = true, Amount = 50m }
        };

        var isLocked = DashboardVM.ShouldLockDashboardForSpendingAmount(sources, logs);

        Assert.False(isLocked);
    }

    [Fact]
    public void ShouldLockDashboardForSpendingAmount_WhenOnlyPositiveSourceIsDisabled_ReturnsTrue()
    {
        var sources = new[]
        {
            new AccountVM
            {
                AccountType = AccountType.Checking,
                Balance = 25m,
                IsEnabled = false
            }
        };

        var isLocked = DashboardVM.ShouldLockDashboardForSpendingAmount(sources, []);

        Assert.True(isLocked);
    }

    [Fact]
    public void ShouldLockActionsForSufficientFunds_WhenNoSources_ReturnsTrue()
    {
        var isLocked = DashboardVM.ShouldLockActionsForSufficientFunds([], []);

        Assert.True(isLocked);
    }

    [Fact]
    public void ShouldLockActionsForSufficientFunds_WhenEnabledSourcesHaveNoPositiveFunds_ReturnsTrue()
    {
        var sources = new[]
        {
            new AccountVM { AccountType = AccountType.Cash, Balance = 0m, IsEnabled = true },
            new AccountVM { AccountType = AccountType.Checking, Balance = -10m, IsEnabled = true },
            new AccountVM { AccountType = AccountType.Saving, Balance = 0m, IsEnabled = true },
            new AccountVM { AccountType = AccountType.Credit, AccountLimit = 0m, IsEnabled = true }
        };

        var isLocked = DashboardVM.ShouldLockActionsForSufficientFunds(sources, []);

        Assert.True(isLocked);
    }

    [Fact]
    public void ShouldLockActionsForSufficientFunds_WhenAnyEnabledNonCreditSourceHasPositiveBalance_ReturnsFalse()
    {
        var sources = new[]
        {
            new AccountVM { AccountType = AccountType.Cash, Balance = 0m, IsEnabled = true },
            new AccountVM { AccountType = AccountType.Checking, Balance = 25m, IsEnabled = true }
        };

        var isLocked = DashboardVM.ShouldLockActionsForSufficientFunds(sources, []);

        Assert.False(isLocked);
    }

    [Fact]
    public void ShouldLockActionsForSufficientFunds_WhenAnyEnabledCreditHasPositiveLimit_ReturnsFalse()
    {
        var sources = new[]
        {
            new AccountVM { AccountType = AccountType.Credit, AccountLimit = 500m, IsEnabled = true }
        };

        var isLocked = DashboardVM.ShouldLockActionsForSufficientFunds(sources, []);

        Assert.False(isLocked);
    }

    [Fact]
    public void ShouldLockActionsForSufficientFunds_WhenOnlyFundedSourceIsDisabled_ReturnsTrue()
    {
        var sources = new[]
        {
            new AccountVM
            {
                AccountType = AccountType.Checking,
                Balance = 25m,
                IsEnabled = false
            }
        };

        var isLocked = DashboardVM.ShouldLockActionsForSufficientFunds(sources, []);

        Assert.True(isLocked);
    }

    [Fact]
    public void ShouldLockActionsForSufficientFunds_WhenEnabledSourcesHaveNoPositiveFundsButHasExpenseLog_ReturnsFalse()
    {
        var sources = new[]
        {
            new AccountVM { AccountType = AccountType.Cash, Balance = 0m, IsEnabled = true }
        };
        var logs = new[]
        {
            new ExpenseLogVM { IsForDeletion = false, Amount = 50m }
        };

        var isLocked = DashboardVM.ShouldLockActionsForSufficientFunds(sources, logs);

        Assert.False(isLocked);
    }

    [Fact]
    public void ShouldLockActionsForSufficientFunds_WhenEnabledSourcesHaveNoPositiveFundsAndOnlyDeletedExpenseLog_ReturnsTrue()
    {
        var sources = new[]
        {
            new AccountVM { AccountType = AccountType.Cash, Balance = 0m, IsEnabled = true }
        };
        var logs = new[]
        {
            new ExpenseLogVM { IsForDeletion = true, Amount = 50m }
        };

        var isLocked = DashboardVM.ShouldLockActionsForSufficientFunds(sources, logs);

        Assert.True(isLocked);
    }
}
