using Fluxo.Core.DTOs;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;

namespace Fluxo.Core.Interfaces.Services;

public interface ISavingsService
{
    // ── Savings accounts ──────────────────────────────────────────────────────
    Task<IReadOnlyList<SavingsAccount>> GetActiveAccountsAsync();

    Task<SavingsAccount> AddAccountAsync(string name, decimal initialBalance,
        decimal annualInterestRate, DateTime? startDate = null, string? notes = null);

    Task UpdateAccountAsync(SavingsAccount account);

    /// <summary>
    /// Deposits or withdraws from an account (positive = deposit, negative = withdrawal).
    /// Stamps UpdatedAt so the interest projection always starts from the right base.
    /// </summary>
    Task<SavingsAccount> AdjustBalanceAsync(int accountId, decimal delta);

    Task DeactivateAccountAsync(int accountId);

    /// <summary>
    /// Compound monthly interest projection for N months:
    ///   monthlyRate = annualRate / 12 / 100
    ///   balance(n)  = balance(0) × (1 + monthlyRate)^n
    ///
    /// Returns one ProjectionPoint per month including month 0 (current balance).
    /// </summary>
    Task<SavingsProjection> ProjectAccountGrowthAsync(int accountId, int months);

    /// <summary>Convenience: 12-month projection for all active accounts.</summary>
    Task<IReadOnlyList<SavingsProjection>> ProjectAllAccountsAsync(int months = 12);

    // ── Savings goals ─────────────────────────────────────────────────────────
    Task<IReadOnlyList<SavingsGoal>> GetActiveGoalsAsync();

    /// <summary>
    /// Creates a goal. Immediately calculates and persists EstimatedCompletionDate.
    /// When <paramref name="startDate"/> is null, uses DefaultEntryDay of current month.
    /// </summary>
    Task<SavingsGoal> AddGoalAsync(string name, decimal targetAmount,
        decimal contributionAmount, ContributionFrequency frequency,
        DateTime? startDate = null, string? notes = null);

    Task UpdateGoalAsync(SavingsGoal goal);

    /// <summary>
    /// Records a contribution to a goal, updates CurrentAmount,
    /// and recalculates EstimatedCompletionDate.
    /// Marks IsCompleted automatically if CurrentAmount >= TargetAmount.
    /// </summary>
    Task<GoalProgressSummary> RecordContributionAsync(int goalId, decimal amount);

    /// <summary>Full progress details including periods remaining and on-track status.</summary>
    Task<GoalProgressSummary> GetGoalProgressAsync(int goalId);

    Task DeactivateGoalAsync(int goalId);

    // ── Calculation helpers (also used by UI for live preview before saving) ──

    /// <summary>
    /// Pure calculation — no DB access.
    /// Estimates how many contribution periods are needed to reach a goal.
    /// </summary>
    int CalculatePeriodsNeeded(decimal current, decimal target, decimal contribution);

    /// <summary>
    /// Pure calculation — no DB access.
    /// Translates a number of contribution periods into a calendar date.
    /// </summary>
    DateTime EstimateCompletionDate(DateTime startDate, ContributionFrequency frequency, int periodsNeeded);
}