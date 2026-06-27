using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.ViewModels.Popups;
using Xunit;

namespace Fluxo.Tests.ViewModels.Popups;

public sealed class AddNewTransactionHistoryBuilderTests
{
    [Fact]
    public void BuildPinnedExpenses_CollapsesDuplicatePinnedItemsKeepingNewest()
    {
        var older = CreateExpenseLog(id: 1, name: "Coffee", amount: 5m, date: DateTime.Today.AddDays(-2), isPinned: true);
        var newer = CreateExpenseLog(id: 2, name: "Coffee", amount: 5m, date: DateTime.Today, isPinned: true);

        var result = AddNewTransactionHistoryBuilder.BuildPinnedExpenses([older, newer]);

        Assert.Single(result);
        Assert.Equal(2, result[0].Id);
        Assert.True(result[0].IsPinned);
    }

    [Fact]
    public void BuildExpenseHistory_ExcludesPinnedAndCollapsesRepeatingItemsKeepingNewest()
    {
        var pinned = CreateExpenseLog(id: 1, name: "Coffee", amount: 5m, date: DateTime.Today.AddDays(-3), isPinned: true);
        var older = CreateExpenseLog(id: 2, name: "Coffee", amount: 5m, date: DateTime.Today.AddDays(-2), isPinned: false);
        var newer = CreateExpenseLog(id: 3, name: "Coffee", amount: 5m, date: DateTime.Today, isPinned: false);

        var result = AddNewTransactionHistoryBuilder.BuildExpenseHistory([pinned, older, newer]);

        Assert.Single(result);
        Assert.Equal(3, result[0].Id);
        Assert.False(result[0].IsPinned);
    }

    [Fact]
    public void BuildExpenseHistory_ExcludesSystemTaggedLogs()
    {
        var log = CreateExpenseLog(
            id: 1,
            name: "Goal update",
            amount: 10m,
            date: DateTime.Today,
            isPinned: false,
            isSystemTag: true);

        var result = AddNewTransactionHistoryBuilder.BuildExpenseHistory([log]);

        Assert.Empty(result);
    }

    [Fact]
    public void BuildPinnedIncomes_CollapsesDuplicatePinnedItemsKeepingNewest()
    {
        var older = CreateIncomeLog(id: 1, name: "Salary", amount: 100m, date: DateTime.Today.AddDays(-2), isPinned: true);
        var newer = CreateIncomeLog(id: 2, name: "Salary", amount: 100m, date: DateTime.Today, isPinned: true);

        var result = AddNewTransactionHistoryBuilder.BuildPinnedIncomes([older, newer]);

        Assert.Single(result);
        Assert.Equal(2, result[0].Id);
        Assert.True(result[0].IsPinned);
    }

    [Fact]
    public void BuildIncomeHistory_ExcludesPinnedAndCollapsesRepeatingItemsKeepingNewest()
    {
        var pinned = CreateIncomeLog(id: 1, name: "Salary", amount: 100m, date: DateTime.Today.AddDays(-3), isPinned: true);
        var older = CreateIncomeLog(id: 2, name: "Salary", amount: 100m, date: DateTime.Today.AddDays(-2), isPinned: false);
        var newer = CreateIncomeLog(id: 3, name: "Salary", amount: 100m, date: DateTime.Today, isPinned: false);

        var result = AddNewTransactionHistoryBuilder.BuildIncomeHistory([pinned, older, newer]);

        Assert.Single(result);
        Assert.Equal(3, result[0].Id);
        Assert.False(result[0].IsPinned);
    }

    [Fact]
    public void BuildGoalUpdateHistory_ReturnsOnlyPastUpdatesForSelectedGoalNewestFirst()
    {
        var selectedOlder = CreateGoalUpdateLog(id: 1, goalName: "Vacation", amount: 10m, date: DateTime.Today.AddDays(-2));
        var selectedNewer = CreateGoalUpdateLog(id: 2, goalName: "Vacation", amount: 15m, date: DateTime.Today);
        var otherGoal = CreateGoalUpdateLog(id: 3, goalName: "Emergency", amount: 20m, date: DateTime.Today.AddDays(-1));
        var normalExpense = CreateExpenseLog(id: 4, name: "Vacation", amount: 5m, date: DateTime.Today, isPinned: false);

        var result = AddNewTransactionHistoryBuilder.BuildGoalUpdateHistory(
            [selectedOlder, selectedNewer, otherGoal, normalExpense],
            "Vacation");

        Assert.Equal([2, 1], result.Select(item => item.Id));
        Assert.All(result, item => Assert.False(item.IsPinned));
    }

    private static ExpenseLog CreateExpenseLog(
        int id,
        string name,
        decimal amount,
        DateTime date,
        bool isPinned,
        bool isSystemTag = false)
    {
        return new ExpenseLog
        {
            Id = id,
            Amount = amount,
            DeductedOn = date,
            Notes = "note",
            IsPinned = isPinned,
            AccountId = 10,
            Account = new Account { Id = 10, Name = "Checking" },
            Expense = new Expense
            {
                Id = id + 100,
                Name = name,
                Amount = amount,
                ExpenseCategory = ExpenseCategory.Needs,
                TagId = 20,
                Tag = new Tag
                {
                    Id = 20,
                    Name = "Food",
                    HexCode = "#22C55E",
                    IsSystemTag = isSystemTag
                }
            }
        };
    }

    private static IncomeLog CreateIncomeLog(
        int id,
        string name,
        decimal amount,
        DateTime date,
        bool isPinned)
    {
        return new IncomeLog
        {
            Id = id,
            Name = name,
            Amount = amount,
            AddedOn = date,
            Notes = "note",
            IsPinned = isPinned,
            AccountId = 10,
            Account = new Account { Id = 10, Name = "Checking" }
        };
    }

    private static ExpenseLog CreateGoalUpdateLog(
        int id,
        string goalName,
        decimal amount,
        DateTime date)
    {
        return new ExpenseLog
        {
            Id = id,
            Amount = amount,
            DeductedOn = date,
            Notes = $"Goal update for {goalName}",
            IsPinned = false,
            AccountId = 10,
            Account = new Account { Id = 10, Name = "Checking" },
            Expense = new Expense
            {
                Id = id + 100,
                Name = $"{GoalUpdateTransactionSupport.GoalUpdateTagName}: {goalName}",
                Amount = amount,
                ExpenseCategory = ExpenseCategory.Savings,
                TagId = 20,
                Tag = new Tag
                {
                    Id = 20,
                    Name = GoalUpdateTransactionSupport.GoalUpdateTagName,
                    HexCode = GoalUpdateTransactionSupport.GoalUpdateTagColor,
                    IsSystemTag = false
                }
            }
        };
    }
}
