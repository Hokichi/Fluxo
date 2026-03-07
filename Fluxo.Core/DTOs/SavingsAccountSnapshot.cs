namespace Fluxo.Core.DTOs;

public sealed class SavingsAccountSnapshot
{
    public int AccountId { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal CurrentBalance { get; init; }
    public decimal AnnualInterestRate { get; init; }

    /// <summary>Projected balance one year from now using compound monthly interest.</summary>
    public decimal ProjectedBalanceIn12Months { get; init; }
}