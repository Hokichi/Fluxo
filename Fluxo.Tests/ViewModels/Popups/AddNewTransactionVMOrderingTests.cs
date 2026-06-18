using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Popups;
using Xunit;

namespace Fluxo.Tests.ViewModels.Popups;

public class AddNewTransactionVMOrderingTests
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

        var projected = AddNewTransactionVM.ProjectNonSystemTags(tags).ToList();

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
    public void Accounts_AreOrderedByTypeThenConfiguredMetric()
    {
        var sources = new[]
        {
            new AccountVM { Id = 1, Name = "Checking Low", AccountType = AccountType.Checking, Balance = 100m },
            new AccountVM { Id = 2, Name = "Checking High", AccountType = AccountType.Checking, Balance = 500m },
            new AccountVM { Id = 3, Name = "Cash Low", AccountType = AccountType.Cash, Balance = 80m },
            new AccountVM { Id = 4, Name = "Cash High", AccountType = AccountType.Cash, Balance = 200m },
            new AccountVM { Id = 5, Name = "Credit Low Remaining", AccountType = AccountType.Credit, AccountLimit = 1000m, SpentAmount = 900m },
            new AccountVM { Id = 6, Name = "Credit High Remaining", AccountType = AccountType.Credit, AccountLimit = 1000m, SpentAmount = 200m },
            new AccountVM { Id = 7, Name = "BNPL Low Remaining", AccountType = AccountType.BNPL, AccountLimit = 500m, SpentAmount = 450m },
            new AccountVM { Id = 8, Name = "BNPL High Remaining", AccountType = AccountType.BNPL, AccountLimit = 500m, SpentAmount = 100m },
            new AccountVM { Id = 9, Name = "Savings Low", AccountType = AccountType.Saving, Balance = 50m },
            new AccountVM { Id = 10, Name = "Savings High", AccountType = AccountType.Saving, Balance = 700m }
        };

        var ordered = sources
            .OrderBy(AddNewTransactionVM.GetAccountTypeSortOrder)
            .ThenByDescending(AddNewTransactionVM.GetAccountWithinTypeSortValue)
            .ThenBy(source => source.Name, StringComparer.OrdinalIgnoreCase)
            .Select(source => source.Id)
            .ToList();

        Assert.Equal([2, 1, 4, 3, 6, 5, 8, 7, 10, 9], ordered);
    }

    [Fact]
    public void BuildTransactionNameSuggestions_MatchesExpenseNamesAnywhere_AndLoadsExpenseData()
    {
        var checking = new Account { Id = 3, Name = "Checking", IsEnabled = true };
        var groceries = new ExpenseTag { Id = 7, Name = "Groceries", HexCode = "#22C55E" };
        var logs = new[]
        {
            new ExpenseLog
            {
                Id = 20,
                Amount = 42.50m,
                DeductedOn = new DateTime(2026, 5, 1),
                Notes = "weekly",
                AccountId = checking.Id,
                Account = checking,
                Expense = new Expense
                {
                    Id = 10,
                    Name = "Market Groceries",
                    Amount = 42.50m,
                    ExpenseCategory = ExpenseCategory.Needs,
                    AccountId = checking.Id,
                    Account = checking,
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
                AccountId = checking.Id,
                Account = checking,
                Expense = new Expense
                {
                    Id = 11,
                    Name = "Coffee",
                    Amount = 12m,
                    ExpenseCategory = ExpenseCategory.Wants,
                    AccountId = checking.Id,
                    Account = checking,
                    ExpenseTagId = groceries.Id,
                    ExpenseTag = groceries
                }
            }
        };

        var suggestions = AddNewTransactionVM.BuildTransactionNameSuggestions(logs, [], isExpense: true, query: "gro").ToList();

        var suggestion = Assert.Single(suggestions);
        Assert.Equal("Market Groceries", suggestion.Name);
        Assert.Equal(42.50m, suggestion.Amount);
        Assert.Equal(3, suggestion.AccountId);
        Assert.Equal(ExpenseCategory.Needs, suggestion.Category);
        Assert.Equal(7, suggestion.TagId);
        Assert.Equal("weekly", suggestion.Note);
        Assert.Null(suggestion.Date);
    }

    [Fact]
    public void BuildTransactionNameSuggestions_MatchesIncomeNamesAnywhere_AndLoadsIncomeData()
    {
        var checking = new Account { Id = 5, Name = "Checking", IsEnabled = true };
        var logs = new[]
        {
            new IncomeLog
            {
                Id = 30,
                Name = "Monthly Salary",
                Amount = 3000m,
                AddedOn = new DateTime(2026, 5, 3),
                Notes = "May payroll",
                AccountId = checking.Id,
                Account = checking
            },
            new IncomeLog
            {
                Id = 31,
                Name = "Refund",
                Amount = 14m,
                AddedOn = new DateTime(2026, 5, 4),
                Notes = "store",
                AccountId = checking.Id,
                Account = checking
            }
        };

        var suggestions = AddNewTransactionVM.BuildTransactionNameSuggestions([], logs, isExpense: false, query: "sal").ToList();

        var suggestion = Assert.Single(suggestions);
        Assert.Equal("Monthly Salary", suggestion.Name);
        Assert.Equal(3000m, suggestion.Amount);
        Assert.Equal(5, suggestion.AccountId);
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
                AccountId = 5,
                Account = new Account { Id = 5, Name = "Checking" }
            }
        };

        var suggestions = AddNewTransactionVM.BuildTransactionNameSuggestions([], logs, isExpense: false, query: "sa");

        Assert.Empty(suggestions);
    }

    [Theory]
    [InlineData(RecurringPeriod.None, "", 0)]
    [InlineData(RecurringPeriod.Weekly, "7", 7)]
    [InlineData(RecurringPeriod.Biweekly, "7", 7)]
    [InlineData(RecurringPeriod.Monthly, "28", 28)]
    public void TryNormalizeRecurringTime_AcceptsValidValues(RecurringPeriod period, string text, int expected)
    {
        var result = AddNewTransactionVM.TryNormalizeRecurringTime(period, text, out var recurringTime);

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
        var result = AddNewTransactionVM.TryNormalizeRecurringTime(period, text, out _);

        Assert.False(result);
    }
}
