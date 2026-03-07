namespace Fluxo.Core.DTOs;

public sealed class IncomeSourceSummary
{
    public int SourceId { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal TotalThisMonth { get; init; }
}