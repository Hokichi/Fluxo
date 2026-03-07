using Fluxo.Core.Enums;

namespace Fluxo.Core.DTOs;

public sealed class GoalProgressSummary
{
    public int GoalId { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal TargetAmount { get; init; }
    public decimal CurrentAmount { get; init; }
    public decimal RemainingAmount => TargetAmount - CurrentAmount;
    public decimal ProgressPercent => TargetAmount == 0 ? 100 : Math.Min(100, CurrentAmount / TargetAmount * 100);

    public decimal ContributionAmount { get; init; }
    public ContributionFrequency Frequency { get; init; }

    public DateTime StartDate { get; init; }
    public DateTime? EstimatedCompletionDate { get; init; }

    /// <summary>Number of contribution periods remaining. Null if already complete.</summary>
    public int? PeriodsRemaining { get; init; }

    public bool IsCompleted { get; init; }
    public bool IsOnTrack { get; init; }
}