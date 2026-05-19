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
        var isLocked = MainVM.ShouldLockDashboardForSpendingAmount([]);

        Assert.True(isLocked);
    }

    [Fact]
    public void ShouldLockDashboardForSpendingAmount_WhenNonCreditSourcesHaveNoPositiveBalance_ReturnsTrue()
    {
        var sources = new[]
        {
            new SpendingSourceVM { SpendingSourceType = SpendingSourceType.Cash, Balance = 0m },
            new SpendingSourceVM { SpendingSourceType = SpendingSourceType.Checking, Balance = -10m },
            new SpendingSourceVM { SpendingSourceType = SpendingSourceType.Saving, Balance = 0m }
        };

        var isLocked = MainVM.ShouldLockDashboardForSpendingAmount(sources);

        Assert.True(isLocked);
    }

    [Fact]
    public void ShouldLockDashboardForSpendingAmount_WhenAnyNonCreditSourceHasPositiveBalance_ReturnsFalse()
    {
        var sources = new[]
        {
            new SpendingSourceVM { SpendingSourceType = SpendingSourceType.Cash, Balance = 0m },
            new SpendingSourceVM { SpendingSourceType = SpendingSourceType.Checking, Balance = 25m }
        };

        var isLocked = MainVM.ShouldLockDashboardForSpendingAmount(sources);

        Assert.False(isLocked);
    }

    [Fact]
    public void ShouldLockDashboardForSpendingAmount_WhenCreditOrBnplHaveNoPositiveLimit_ReturnsTrue()
    {
        var sources = new[]
        {
            new SpendingSourceVM { SpendingSourceType = SpendingSourceType.Credit, AccountLimit = 0m },
            new SpendingSourceVM { SpendingSourceType = SpendingSourceType.BNPL, AccountLimit = -1m }
        };

        var isLocked = MainVM.ShouldLockDashboardForSpendingAmount(sources);

        Assert.True(isLocked);
    }

    [Fact]
    public void ShouldLockDashboardForSpendingAmount_WhenAnyCreditOrBnplHasPositiveLimit_ReturnsFalse()
    {
        var sources = new[]
        {
            new SpendingSourceVM { SpendingSourceType = SpendingSourceType.Credit, AccountLimit = 0m },
            new SpendingSourceVM { SpendingSourceType = SpendingSourceType.BNPL, AccountLimit = 500m }
        };

        var isLocked = MainVM.ShouldLockDashboardForSpendingAmount(sources);

        Assert.False(isLocked);
    }
}
