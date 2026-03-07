namespace Fluxo.Core.DTOs;

public sealed class ProjectionPoint
{
    public int Month { get; init; }
    public int Year { get; init; }
    public decimal Balance { get; init; }

    /// <summary>Interest earned in this specific month.</summary>
    public decimal InterestEarned { get; init; }
}