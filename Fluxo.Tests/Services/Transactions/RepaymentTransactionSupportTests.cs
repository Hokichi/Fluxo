using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Services.Transactions;
using Xunit;

namespace Fluxo.Tests.Services.Transactions;

public sealed class RepaymentTransactionSupportTests
{
    [Fact]
    public void Create_ProducesExcludedBalanceUpdatePair_AndMutatesAccounts()
    {
        var checking = new Account
        {
            Id = 1,
            Name = "Checking",
            AccountType = AccountType.Checking,
            Balance = 500m
        };
        var credit = new Account
        {
            Id = 2,
            Name = "Visa",
            AccountType = AccountType.Credit,
            SpentAmount = 200m
        };
        var tag = new Tag { Id = 3, Name = "Balance Update", HexCode = "#fff" };

        var pair = RepaymentTransactionSupport.Create(
            checking,
            credit,
            75m,
            new DateTime(2026, 6, 28),
            tag);

        Assert.Equal(425m, checking.Balance);
        Assert.Equal(125m, credit.SpentAmount);
        Assert.Equal(TransactionType.Expense, pair.Expense.Type);
        Assert.Equal("Repayment to Visa", pair.Expense.Name);
        Assert.Equal(ExpenseCategory.Savings, pair.Expense.ExpenseCategory);
        Assert.Equal(3, pair.Expense.TagId);
        Assert.True(pair.Expense.IsExcludedFromBudget);
        Assert.Equal(TransactionType.Income, pair.Income.Type);
        Assert.Equal("Repayment from Checking", pair.Income.Name);
        Assert.Equal(2, pair.Income.AccountId);
        Assert.True(pair.Income.IsExcludedFromBudget);
    }

    [Theory]
    [InlineData(AccountType.Cash, AccountType.Credit, 10)]
    [InlineData(AccountType.Checking, AccountType.Checking, 10)]
    [InlineData(AccountType.Checking, AccountType.Credit, 0)]
    [InlineData(AccountType.Checking, AccountType.Credit, 101)]
    public void Create_RejectsInvalidRepayment(
        AccountType sourceType,
        AccountType targetType,
        decimal amount)
    {
        var source = new Account { AccountType = sourceType };
        var target = new Account { AccountType = targetType, SpentAmount = 100m };
        var tag = new Tag { Id = 1, Name = "Balance Update", HexCode = "#fff" };

        Assert.Throws<ArgumentException>(() =>
            RepaymentTransactionSupport.Create(source, target, amount, DateTime.Today, tag));
    }
}
