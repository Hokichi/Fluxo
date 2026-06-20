using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Services.History;
using Xunit;

namespace Fluxo.Tests.Services.History;

public sealed class LogMemorySnapshotTests
{
    [Fact]
    public void Create_CapturesDebtIouFlags()
    {
        var account = new Account { Id = 1, Name = "Checking" };
        var tag = new ExpenseTag { Id = 2, Name = "Budget Reconciliation", HexCode = "#9ca3af" };
        var expense = new Expense
        {
            Id = 3,
            AccountId = account.Id,
            Account = account,
            ExpenseTagId = tag.Id,
            ExpenseTag = tag,
            Name = "Lend",
            Amount = 10m,
            ExpenseCategory = ExpenseCategory.Needs,
            IsLend = true
        };
        var expenseLog = new ExpenseLog
        {
            Id = 4,
            Expense = expense,
            Account = account,
            Amount = 10m,
            Notes = string.Empty,
            IsLend = true
        };
        var incomeLog = new IncomeLog
        {
            Id = 5,
            Account = account,
            Name = "Debt",
            Amount = 10m,
            Notes = string.Empty,
            IsDebt = true
        };

        Assert.True(ExpenseMemorySnapshot.Create(expense).IsLend);
        Assert.True(ExpenseLogMemorySnapshot.Create(expenseLog).IsLend);
        Assert.True(IncomeLogMemorySnapshot.Create(incomeLog).IsDebt);
    }
}
