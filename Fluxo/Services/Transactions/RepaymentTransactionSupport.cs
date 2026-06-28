using Fluxo.Core.Entities;
using Fluxo.Core.Enums;

namespace Fluxo.Services.Transactions;

public static class RepaymentTransactionSupport
{
    public static RepaymentTransactionPair Create(
        Account source,
        Account target,
        decimal amount,
        DateTime occurredOn,
        Tag balanceUpdateTag,
        string? expenseName = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(balanceUpdateTag);

        if (source.AccountType != AccountType.Checking)
            throw new ArgumentException("Repayment source must be a checking account.", nameof(source));
        if (target.AccountType != AccountType.Credit)
            throw new ArgumentException("Repayment target must be a credit account.", nameof(target));
        if (amount <= 0m || amount > target.SpentAmount)
            throw new ArgumentException(
                "Repayment amount must be positive and not exceed spent amount.",
                nameof(amount));

        var expense = new Transaction
        {
            Type = TransactionType.Expense,
            Name = string.IsNullOrWhiteSpace(expenseName)
                ? $"Repayment to {target.Name}"
                : expenseName.Trim(),
            Amount = amount,
            OccurredOn = occurredOn,
            Notes = string.Empty,
            ExpenseCategory = ExpenseCategory.Savings,
            AccountId = source.Id,
            TagId = balanceUpdateTag.Id,
            IsExcludedFromBudget = true
        };
        var income = new Transaction
        {
            Type = TransactionType.Income,
            Name = $"Repayment from {source.Name}",
            Amount = amount,
            OccurredOn = occurredOn,
            Notes = string.Empty,
            AccountId = target.Id,
            IsExcludedFromBudget = true
        };

        source.Balance -= amount;
        target.SpentAmount -= amount;

        return new RepaymentTransactionPair(expense, income);
    }
}

public readonly record struct RepaymentTransactionPair(Transaction Expense, Transaction Income);
