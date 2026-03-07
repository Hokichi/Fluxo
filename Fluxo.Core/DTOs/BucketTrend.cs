using Fluxo.Core.Enums;

namespace Fluxo.Core.DTOs;

public sealed class BucketTrend
{
    public ExpenseCategory Category { get; init; }
    public decimal TotalSpent { get; init; }
    public decimal AverageMonthly { get; init; }
    public decimal PercentOfIncome { get; init; }
}