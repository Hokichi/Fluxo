namespace Fluxo.Core.DTOs;

public sealed class TagSpendSummary
{
    public int TagId { get; init; }
    public string TagName { get; init; } = string.Empty;
    public string TagColor { get; init; } = "#808080";
    public decimal TotalSpent { get; init; }
    public int TransactionCount { get; init; }
    public decimal PercentOfTotalExpenses { get; init; }
}