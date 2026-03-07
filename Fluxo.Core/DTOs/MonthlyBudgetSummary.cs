namespace Fluxo.Core.DTOs;

public sealed class MonthlyBudgetSummary
{
    public int Month { get; init; }
    public int Year { get; init; }

    /// <summary>Sum of all income entries for the month.</summary>
    public decimal TotalIncome { get; init; }

    /// <summary>
    /// Total BNPL set-aside amounts — displayed in grey next to real income.
    /// Does NOT reduce TotalIncome for bucket calculation; it's a visual reminder.
    /// </summary>
    public decimal BnplSetAsideTotal { get; init; }

    public BudgetBucket Needs { get; init; } = new();
    public BudgetBucket Wants { get; init; } = new();
    public BudgetBucket Savings { get; init; } = new();

    /// <summary>Money that has been neither spent nor allocated to any bucket yet.</summary>
    public decimal IdleMoney => TotalIncome - Needs.Allocated - Wants.Allocated - Savings.Allocated;

    /// <summary>Convenience: remaining across all buckets combined.</summary>
    public decimal TotalRemaining => Needs.Remaining + Wants.Remaining + Savings.Remaining;
}