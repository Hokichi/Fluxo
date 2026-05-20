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

    [Fact]
    public void BuildTransactionNameSuggestions_MatchesExpenseNamesAnywhere_AndLoadsExpenseData()
    {
        var checking = new SpendingSource { Id = 3, Name = "Checking", IsEnabled = true };
        var groceries = new ExpenseTag { Id = 7, Name = "Groceries", HexCode = "#22C55E" };
        var logs = new[]
        {
            new ExpenseLog
            {
                Id = 20,
                Amount = 42.50m,
                DeductedOn = new DateTime(2026, 5, 1),
                Notes = "weekly",
                SpendingSourceId = checking.Id,
                SpendingSource = checking,
                Expense = new Expense
                {
                    Id = 10,
                    Name = "Market Groceries",
                    Amount = 42.50m,
                    ExpenseCategory = ExpenseCategory.Needs,
                    SpendingSourceId = checking.Id,
                    SpendingSource = checking,
                    ExpenseTagId = groceries.Id,
                    ExpenseTag = groceries
                }
            },
            new ExpenseLog
            {
                Id = 21,
                Amount = 12m,
                DeductedOn = new DateTime(2026, 5, 2),
                Notes = "coffee",
                SpendingSourceId = checking.Id,
                SpendingSource = checking,
                Expense = new Expense
                {
                    Id = 11,
                    Name = "Coffee",
                    Amount = 12m,
                    ExpenseCategory = ExpenseCategory.Wants,
                    SpendingSourceId = checking.Id,
                    SpendingSource = checking,
                    ExpenseTagId = groceries.Id,
                    ExpenseTag = groceries
                }
            }
        };

        var suggestions = QuickAddVM.BuildTransactionNameSuggestions(logs, [], isExpense: true, query: "gro").ToList();

        var suggestion = Assert.Single(suggestions);
        Assert.Equal("Market Groceries", suggestion.Name);
        Assert.Equal(42.50m, suggestion.Amount);
        Assert.Equal(3, suggestion.SpendingSourceId);
        Assert.Equal(ExpenseCategory.Needs, suggestion.Category);
        Assert.Equal(7, suggestion.TagId);
        Assert.Equal("weekly", suggestion.Note);
        Assert.Null(suggestion.Date);
    }

    [Fact]
    public void BuildTransactionNameSuggestions_MatchesIncomeNamesAnywhere_AndLoadsIncomeData()
    {
        var checking = new SpendingSource { Id = 5, Name = "Checking", IsEnabled = true };
        var logs = new[]
        {
            new IncomeLog
            {
                Id = 30,
                Name = "Monthly Salary",
                Amount = 3000m,
                AddedOn = new DateTime(2026, 5, 3),
                Notes = "May payroll",
                SpendingSourceId = checking.Id,
                SpendingSource = checking
            },
            new IncomeLog
            {
                Id = 31,
                Name = "Refund",
                Amount = 14m,
                AddedOn = new DateTime(2026, 5, 4),
                Notes = "store",
                SpendingSourceId = checking.Id,
                SpendingSource = checking
            }
        };

        var suggestions = QuickAddVM.BuildTransactionNameSuggestions([], logs, isExpense: false, query: "sal").ToList();

        var suggestion = Assert.Single(suggestions);
        Assert.Equal("Monthly Salary", suggestion.Name);
        Assert.Equal(3000m, suggestion.Amount);
        Assert.Equal(5, suggestion.SpendingSourceId);
        Assert.Null(suggestion.Category);
        Assert.Null(suggestion.TagId);
        Assert.Equal("May payroll", suggestion.Note);
        Assert.Null(suggestion.Date);
    }

    [Fact]
    public void BuildTransactionNameSuggestions_RequiresAtLeastThreeCharacters()
    {
        var logs = new[]
        {
            new IncomeLog
            {
                Id = 30,
                Name = "Monthly Salary",
                Amount = 3000m,
                AddedOn = new DateTime(2026, 5, 3),
                Notes = "May payroll",
                SpendingSourceId = 5,
                SpendingSource = new SpendingSource { Id = 5, Name = "Checking" }
            }
        };

        var suggestions = QuickAddVM.BuildTransactionNameSuggestions([], logs, isExpense: false, query: "sa");

        Assert.Empty(suggestions);
    }

    [Theory]
    [InlineData(RecurringPeriod.None, "", 0)]
    [InlineData(RecurringPeriod.Weekly, "7", 7)]
    [InlineData(RecurringPeriod.Biweekly, "7", 7)]
    [InlineData(RecurringPeriod.Monthly, "28", 28)]
    public void TryNormalizeRecurringTime_AcceptsValidValues(RecurringPeriod period, string text, int expected)
    {
        var result = QuickAddVM.TryNormalizeRecurringTime(period, text, out var recurringTime);

        Assert.True(result);
        Assert.Equal(expected, recurringTime);
    }

    [Theory]
    [InlineData(RecurringPeriod.Weekly, "8")]
    [InlineData(RecurringPeriod.Biweekly, "8")]
    [InlineData(RecurringPeriod.Monthly, "29")]
    [InlineData(RecurringPeriod.Monthly, "0")]
    public void TryNormalizeRecurringTime_RejectsInvalidValues(RecurringPeriod period, string text)
    {
        var result = QuickAddVM.TryNormalizeRecurringTime(period, text, out _);

        Assert.False(result);
    }
}
