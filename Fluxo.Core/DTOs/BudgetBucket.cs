using Fluxo.Core.Enums;

namespace Fluxo.Core.DTOs;

public sealed class BudgetBucket
{
    public ExpenseCategory Category { get; init; }
    public decimal Percentage { get; init; }

    /// <summary>TotalIncome * (Percentage / 100)</summary>
    public decimal Allocated { get; init; }

    /// <summary>Actual amount spent in this bucket this month.</summary>
    public decimal Spent { get; init; }

    public decimal Remaining => Allocated - Spent;

    /// <summary>True when spending has exceeded the allocation.</summary>
    public bool IsOverBudget => Spent > Allocated;

    /// <summary>0–100 progress bar value. Clamped to 100 when over budget.</summary>
    public decimal UsagePercent => Allocated == 0 ? 100 : Math.Min(100, Spent / Allocated * 100);
}