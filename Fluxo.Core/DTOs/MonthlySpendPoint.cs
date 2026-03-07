namespace Fluxo.Core.DTOs;

public sealed class MonthlySpendPoint
{
    public int Month { get; init; }
    public int Year { get; init; }
    public decimal Income { get; init; }
    public decimal Expenses { get; init; }
    public decimal FixedExpenses { get; init; }
    public decimal Savings { get; init; }
}