using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Popups;
using Xunit;

namespace Fluxo.Tests.ViewModels.Popups;

public class QuickAddVMOrderingTests
{
    [Fact]
    public void ProjectNonSystemTags_FiltersSystemTags_AndOrdersByName()
    {
        var tags = new[]
        {
            new ExpenseTag { Id = 1, Name = "zeta", HexCode = "#111111", IsSystemTag = false },
            new ExpenseTag { Id = 2, Name = "Alpha", HexCode = "#222222", IsSystemTag = false },
            new ExpenseTag { Id = 3, Name = "System", HexCode = "#333333", IsSystemTag = true }
        };

        var projected = QuickAddVM.ProjectNonSystemTags(tags).ToList();

        Assert.Collection(projected,
            first =>
            {
                Assert.Equal(2, first.Id);
                Assert.Equal("Alpha", first.Name);
                Assert.False(first.IsSystemTag);
            },
            second =>
            {
                Assert.Equal(1, second.Id);
                Assert.Equal("zeta", second.Name);
                Assert.False(second.IsSystemTag);
            });
    }

    [Fact]
    public void SpendingSources_AreOrderedByTypeThenConfiguredMetric()
    {
        var sources = new[]
        {
            new SpendingSourceVM { Id = 1, Name = "Checking Low", SpendingSourceType = SpendingSourceType.Checking, Balance = 100m },
            new SpendingSourceVM { Id = 2, Name = "Checking High", SpendingSourceType = SpendingSourceType.Checking, Balance = 500m },
            new SpendingSourceVM { Id = 3, Name = "Cash Low", SpendingSourceType = SpendingSourceType.Cash, Balance = 80m },
            new SpendingSourceVM { Id = 4, Name = "Cash High", SpendingSourceType = SpendingSourceType.Cash, Balance = 200m },
            new SpendingSourceVM { Id = 5, Name = "Credit Low Remaining", SpendingSourceType = SpendingSourceType.Credit, AccountLimit = 1000m, SpentAmount = 900m },
            new SpendingSourceVM { Id = 6, Name = "Credit High Remaining", SpendingSourceType = SpendingSourceType.Credit, AccountLimit = 1000m, SpentAmount = 200m },
            new SpendingSourceVM { Id = 7, Name = "BNPL Low Remaining", SpendingSourceType = SpendingSourceType.BNPL, AccountLimit = 500m, SpentAmount = 450m },
            new SpendingSourceVM { Id = 8, Name = "BNPL High Remaining", SpendingSourceType = SpendingSourceType.BNPL, AccountLimit = 500m, SpentAmount = 100m },
            new SpendingSourceVM { Id = 9, Name = "Savings Low", SpendingSourceType = SpendingSourceType.Saving, Balance = 50m },
            new SpendingSourceVM { Id = 10, Name = "Savings High", SpendingSourceType = SpendingSourceType.Saving, Balance = 700m }
        };

        var ordered = sources
            .OrderBy(QuickAddVM.GetSpendingSourceTypeSortOrder)
            .ThenByDescending(QuickAddVM.GetSpendingSourceWithinTypeSortValue)
            .ThenBy(source => source.Name, StringComparer.OrdinalIgnoreCase)
            .Select(source => source.Id)
            .ToList();

        Assert.Equal([2, 1, 4, 3, 6, 5, 8, 7, 10, 9], ordered);
    }
}
