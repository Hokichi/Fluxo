using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.ViewModels.Popups.Helpers;
using Xunit;

namespace Fluxo.Tests.ViewModels.Popups.Helpers;

public sealed class SpendingSourceDeletionConfirmationHelperTests
{
    [Fact]
    public void IsOnlyFunctioningSource_WhenSingleEnabledPositiveSource_ReturnsTrue()
    {
        var sources = new[]
        {
            CreateSource(1, "Main", SpendingSourceType.Checking, isEnabled: true, balance: 100m),
            CreateSource(2, "Backup", SpendingSourceType.Checking, isEnabled: true, balance: 0m)
        };

        var isOnlyFunctioning = SpendingSourceDeletionConfirmationHelper.IsOnlyFunctioningSource(sources, 1);

        Assert.True(isOnlyFunctioning);
    }

    [Fact]
    public void IsOnlyFunctioningSource_WhenMultipleEnabledPositiveSources_ReturnsFalse()
    {
        var sources = new[]
        {
            CreateSource(1, "Main", SpendingSourceType.Checking, isEnabled: true, balance: 100m),
            CreateSource(2, "Backup", SpendingSourceType.Cash, isEnabled: true, balance: 100m)
        };

        var isOnlyFunctioning = SpendingSourceDeletionConfirmationHelper.IsOnlyFunctioningSource(sources, 1);

        Assert.False(isOnlyFunctioning);
    }

    private static SpendingSource CreateSource(
        int id,
        string name,
        SpendingSourceType sourceType,
        bool isEnabled,
        decimal balance)
    {
        return new SpendingSource
        {
            Id = id,
            Name = name,
            SpendingSourceType = sourceType,
            Balance = balance,
            IsEnabled = isEnabled
        };
    }
}
