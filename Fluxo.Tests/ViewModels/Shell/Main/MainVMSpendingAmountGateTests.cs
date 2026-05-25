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
        var isLocked = MainVM.ShouldLockDashboardForSpendingAmount([], []);

        Assert.True(isLocked);
    }

    [Fact]
    public void ShouldLockDashboardForSpendingAmount_WhenNonCreditSourcesHaveNoPositiveBalance_ReturnsTrue()
    {
        var sources = new[]
        {
            new SpendingSourceVM { SpendingSourceType = SpendingSourceType.Cash, Balance = 0m, IsEnabled = true },
            new SpendingSourceVM { SpendingSourceType = SpendingSourceType.Checking, Balance = -10m, IsEnabled = true },
            new SpendingSourceVM { SpendingSourceType = SpendingSourceType.Saving, Balance = 0m, IsEnabled = true }
        };

        var isLocked = MainVM.ShouldLockDashboardForSpendingAmount(sources, []);

        Assert.True(isLocked);
    }

    [Fact]
    public void ShouldLockDashboardForSpendingAmount_WhenAnyNonCreditSourceHasPositiveBalance_ReturnsFalse()
    {
        var sources = new[]
        {
            new SpendingSourceVM { SpendingSourceType = SpendingSourceType.Cash, Balance = 0m, IsEnabled = true },
            new SpendingSourceVM { SpendingSourceType = SpendingSourceType.Checking, Balance = 25m, IsEnabled = true }
        };

        var isLocked = MainVM.ShouldLockDashboardForSpendingAmount(sources, []);

        Assert.False(isLocked);
    }

    [Fact]
    public void ShouldLockDashboardForSpendingAmount_WhenCreditOrBnplHaveNoPositiveLimit_ReturnsTrue()
    {
        var sources = new[]
        {
            new SpendingSourceVM { SpendingSourceType = SpendingSourceType.Credit, AccountLimit = 0m, IsEnabled = true },
            new SpendingSourceVM { SpendingSourceType = SpendingSourceType.BNPL, AccountLimit = -1m, IsEnabled = true }
        };

        var isLocked = MainVM.ShouldLockDashboardForSpendingAmount(sources, []);

        Assert.True(isLocked);
    }

    [Fact]
    public void ShouldLockDashboardForSpendingAmount_WhenAnyCreditOrBnplHasPositiveLimit_ReturnsFalse()
    {
        var sources = new[]
        {
            new SpendingSourceVM { SpendingSourceType = SpendingSourceType.Credit, AccountLimit = 0m, IsEnabled = true },
            new SpendingSourceVM { SpendingSourceType = SpendingSourceType.BNPL, AccountLimit = 500m, IsEnabled = true }
        };

        var isLocked = MainVM.ShouldLockDashboardForSpendingAmount(sources, []);

        Assert.False(isLocked);
    }

    [Fact]
    public void ShouldLockDashboardForSpendingAmount_WhenNoSourcesButHasExpenseLog_ReturnsTrue()
    {
        var logs = new[]
        {
            new ExpenseLogVM { IsForDeletion = false, Amount = 10m }
        };

        var isLocked = MainVM.ShouldLockDashboardForSpendingAmount([], logs);

        Assert.True(isLocked);
    }

    [Fact]
    public void ShouldLockDashboardForSpendingAmount_WhenNonCreditSourcesHaveNoPositiveBalanceButHasExpenseLog_ReturnsFalse()
    {
        var sources = new[]
        {
            new SpendingSourceVM { SpendingSourceType = SpendingSourceType.Cash, Balance = 0m, IsEnabled = true }
        };
        var logs = new[]
        {
            new ExpenseLogVM { IsForDeletion = false, Amount = 50m }
        };

        var isLocked = MainVM.ShouldLockDashboardForSpendingAmount(sources, logs);

        Assert.False(isLocked);
    }

    [Fact]
    public void ShouldLockDashboardForSpendingAmount_WhenHasExpenseLogMarkedForDeletion_ReturnsTrue()
    {
        var sources = new[]
        {
            new SpendingSourceVM { SpendingSourceType = SpendingSourceType.Cash, Balance = 0m, IsEnabled = true }
        };
        var logs = new[]
        {
            new ExpenseLogVM { IsForDeletion = true, Amount = 50m }
        };

        var isLocked = MainVM.ShouldLockDashboardForSpendingAmount(sources, logs);

        Assert.True(isLocked);
    }

    [Fact]
    public void ShouldLockDashboardForSpendingAmount_WhenOnlyPositiveSourceIsDisabled_ReturnsTrue()
    {
        var sources = new[]
        {
            new SpendingSourceVM
            {
                SpendingSourceType = SpendingSourceType.Checking,
                Balance = 25m,
                IsEnabled = false
            }
        };

        var isLocked = MainVM.ShouldLockDashboardForSpendingAmount(sources, []);

        Assert.True(isLocked);
    }
}
