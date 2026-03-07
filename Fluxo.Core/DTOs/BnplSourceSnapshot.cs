namespace Fluxo.Core.DTOs;

public sealed class BnplSourceSnapshot
{
    public int SourceId { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal CurrentBalance { get; init; }
    public decimal? CreditLimit { get; init; }
    public decimal? UtilizationPercent => CreditLimit.HasValue && CreditLimit > 0
        ? CurrentBalance / CreditLimit.Value * 100 : null;

    /// <summary>How much of the current balance is set aside from this month's income.</summary>
    public decimal SetAsideThisMonth { get; init; }
}