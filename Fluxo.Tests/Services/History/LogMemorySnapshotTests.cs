using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Services.History;
using Xunit;

namespace Fluxo.Tests.Services.History;

public sealed class LogMemorySnapshotTests
{
    [Fact]
    public void Create_CapturesIoUFlags()
    {
        var account = new Account { Id = 1, Name = "Checking" };
        var tag = new Tag { Id = 2, Name = "Budget Reconciliation", HexCode = "#9ca3af" };
        var expense = new Expense
        {
            Id = 3,
            AccountId = account.Id,
            Account = account,
            TagId = tag.Id,
            Tag = tag,
            Name = "Lend",
            Amount = 10m,
            ExpenseCategory = ExpenseCategory.Needs,
            IsIoU = true
        };
        var expenseLog = new ExpenseLog
        {
            Id = 4,
            Expense = expense,
            Account = account,
            Amount = 10m,
            Notes = string.Empty,
            IsIoU = true
        };
        var incomeLog = new IncomeLog
        {
            Id = 5,
            Account = account,
            Name = "Debt",
            Amount = 10m,
            Notes = string.Empty,
            IsIoU = true
        };

        Assert.True(ExpenseMemorySnapshot.Create(expense).IsIoU);
        Assert.True(ExpenseLogMemorySnapshot.Create(expenseLog).IsIoU);
        Assert.True(IncomeLogMemorySnapshot.Create(incomeLog).IsIoU);
    }
}
