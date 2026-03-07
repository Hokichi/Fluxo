namespace Fluxo.Core.DTOs;

public sealed class SavingsProjection
{
    public int SavingsAccountId { get; init; }
    public string AccountName { get; init; } = string.Empty;
    public decimal AnnualInterestRate { get; init; }

    /// <summary>
    /// Ordered list of (Month, Year, ProjectedBalance) data points.
    /// Index 0 = current month (balance as of today).
    /// </summary>
    public IReadOnlyList<ProjectionPoint> Points { get; init; } = [];

    /// <summary>Helper: balance at the end of the projection window.</summary>
    public decimal FinalBalance => Points.Count > 0 ? Points[^1].Balance : 0;

    /// <summary>Total interest earned over the projection window.</summary>
    public decimal TotalInterestEarned => FinalBalance - (Points.Count > 0 ? Points[0].Balance : 0);
}