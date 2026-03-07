using Fluxo.Core.DTOs;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Core.Interfaces.Services;

namespace Fluxo.Services.Persistence;

public sealed class SavingsService : ISavingsService
{
    private readonly ISavingsAccountRepository _accounts;
    private readonly ISavingsGoalRepository _goals;
    private readonly IAppSettingService _settings;

    public SavingsService(
        ISavingsAccountRepository accounts,
        ISavingsGoalRepository goals,
        IAppSettingService settings)
    {
        _accounts = accounts;
        _goals = goals;
        _settings = settings;
    }

    // ── Accounts ──────────────────────────────────────────────────────────────

    public Task<IReadOnlyList<SavingsAccount>> GetActiveAccountsAsync()
    {
        return _accounts.GetAllActiveAsync();
    }

    public async Task<SavingsAccount> AddAccountAsync(string name, decimal initialBalance,
        decimal annualInterestRate, DateTime? startDate = null, string? notes = null)
    {
        var account = new SavingsAccount
        {
            Name = name,
            InitialBalance = initialBalance,
            CurrentBalance = initialBalance,
            AnnualInterestRate = annualInterestRate,
            StartDate = startDate ?? await _settings.GetDefaultEntryDateAsync(),
            Notes = notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _accounts.AddAsync(account);
        await _accounts.SaveChangesAsync();
        return account;
    }

    public async Task UpdateAccountAsync(SavingsAccount account)
    {
        account.UpdatedAt = DateTime.UtcNow;
        await _accounts.UpdateAsync(account);
        await _accounts.SaveChangesAsync();
    }

    public async Task<SavingsAccount> AdjustBalanceAsync(int accountId, decimal delta)
    {
        var account = await _accounts.GetByIdAsync(accountId)
                      ?? throw new InvalidOperationException($"SavingsAccount {accountId} not found.");

        await _accounts.UpdateBalanceAsync(accountId, account.CurrentBalance + delta);
        await _accounts.SaveChangesAsync();

        return (await _accounts.GetByIdAsync(accountId))!;
    }

    public async Task DeactivateAccountAsync(int accountId)
    {
        await _accounts.DeactivateAsync(accountId);
        await _accounts.SaveChangesAsync();
    }

    /// <summary>
    ///     Compound monthly interest formula:
    ///     monthlyRate = annualRate / 12 / 100
    ///     balance(n) = balance(0) × (1 + monthlyRate)^n
    /// </summary>
    public async Task<SavingsProjection> ProjectAccountGrowthAsync(int accountId, int months)
    {
        var account = await _accounts.GetByIdAsync(accountId)
                      ?? throw new InvalidOperationException($"SavingsAccount {accountId} not found.");

        return BuildProjection(account, months);
    }

    public async Task<IReadOnlyList<SavingsProjection>> ProjectAllAccountsAsync(int months = 12)
    {
        var accounts = await _accounts.GetAllActiveAsync();
        return accounts.Select(a => BuildProjection(a, months)).ToList();
    }

    private static SavingsProjection BuildProjection(SavingsAccount account, int months)
    {
        var monthlyRate = (double)(account.AnnualInterestRate / 12 / 100);
        var today = DateTime.Today;
        var points = new List<ProjectionPoint>(months + 1);
        var prev = account.CurrentBalance;

        for (var n = 0; n <= months; n++)
        {
            var projected = account.CurrentBalance * (decimal)Math.Pow(1 + monthlyRate, n);
            projected = Math.Round(projected, 2);
            var interest = projected - prev;
            var date = today.AddMonths(n);
            points.Add(new ProjectionPoint
            {
                Month = date.Month,
                Year = date.Year,
                Balance = projected,
                InterestEarned = n == 0 ? 0m : Math.Round(interest, 2)
            });
            prev = projected;
        }

        return new SavingsProjection
        {
            SavingsAccountId = account.Id,
            AccountName = account.Name,
            AnnualInterestRate = account.AnnualInterestRate,
            Points = points
        };
    }

    // ── Goals ─────────────────────────────────────────────────────────────────

    public Task<IReadOnlyList<SavingsGoal>> GetActiveGoalsAsync()
    {
        return _goals.GetAllActiveAsync();
    }

    public async Task<SavingsGoal> AddGoalAsync(string name, decimal targetAmount,
        decimal contributionAmount, ContributionFrequency frequency,
        DateTime? startDate = null, string? notes = null)
    {
        var start = startDate ?? await _settings.GetDefaultEntryDateAsync();
        var periodsNeeded = CalculatePeriodsNeeded(0, targetAmount, contributionAmount);
        var estimatedCompletion = EstimateCompletionDate(start, frequency, periodsNeeded);

        var goal = new SavingsGoal
        {
            Name = name,
            TargetAmount = targetAmount,
            CurrentAmount = 0,
            ContributionAmount = contributionAmount,
            ContributionFrequency = frequency,
            StartDate = start,
            IsManualDate = startDate.HasValue,
            EstimatedCompletionDate = estimatedCompletion,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _goals.AddAsync(goal);
        await _goals.SaveChangesAsync();
        return goal;
    }

    public async Task UpdateGoalAsync(SavingsGoal goal)
    {
        // Recalculate completion date whenever the goal is edited.
        var periodsNeeded = CalculatePeriodsNeeded(
            goal.CurrentAmount, goal.TargetAmount, goal.ContributionAmount);
        goal.EstimatedCompletionDate = EstimateCompletionDate(
            goal.StartDate, goal.ContributionFrequency, periodsNeeded);
        goal.UpdatedAt = DateTime.UtcNow;

        await _goals.UpdateAsync(goal);
        await _goals.SaveChangesAsync();
    }

    public async Task<GoalProgressSummary> RecordContributionAsync(int goalId, decimal amount)
    {
        var goal = await _goals.GetByIdAsync(goalId)
                   ?? throw new InvalidOperationException($"SavingsGoal {goalId} not found.");

        var newAmount = goal.CurrentAmount + amount;

        if (newAmount >= goal.TargetAmount)
        {
            await _goals.MarkCompletedAsync(goalId, DateTime.UtcNow);
        }
        else
        {
            await _goals.UpdateProgressAsync(goalId, newAmount);

            // Recalculate stored EstimatedCompletionDate.
            var periods = CalculatePeriodsNeeded(newAmount, goal.TargetAmount, goal.ContributionAmount);
            goal.EstimatedCompletionDate = EstimateCompletionDate(
                DateTime.Today, goal.ContributionFrequency, periods);
            goal.CurrentAmount = newAmount;
            await _goals.UpdateAsync(goal);
            await _goals.SaveChangesAsync();
        }

        return await GetGoalProgressAsync(goalId);
    }

    public async Task<GoalProgressSummary> GetGoalProgressAsync(int goalId)
    {
        var goal = await _goals.GetByIdAsync(goalId)
                   ?? throw new InvalidOperationException($"SavingsGoal {goalId} not found.");

        int? periodsRemaining = goal.IsCompleted
            ? null
            : CalculatePeriodsNeeded(goal.CurrentAmount, goal.TargetAmount, goal.ContributionAmount);

        // "On track" = EstimatedCompletionDate hasn't slipped beyond the
        // original implied completion from StartDate.
        var isOnTrack = goal.IsCompleted || (goal.EstimatedCompletionDate.HasValue
                                             && goal.EstimatedCompletionDate.Value >= DateTime.Today);

        return new GoalProgressSummary
        {
            GoalId = goal.Id,
            Name = goal.Name,
            TargetAmount = goal.TargetAmount,
            CurrentAmount = goal.CurrentAmount,
            ContributionAmount = goal.ContributionAmount,
            Frequency = goal.ContributionFrequency,
            StartDate = goal.StartDate,
            EstimatedCompletionDate = goal.EstimatedCompletionDate,
            PeriodsRemaining = periodsRemaining,
            IsCompleted = goal.IsCompleted,
            IsOnTrack = isOnTrack
        };
    }

    public async Task DeactivateGoalAsync(int goalId)
    {
        await _goals.DeactivateAsync(goalId);
        await _goals.SaveChangesAsync();
    }

    // ── Pure calculation helpers ──────────────────────────────────────────────

    public int CalculatePeriodsNeeded(decimal current, decimal target, decimal contribution)
    {
        if (contribution <= 0) throw new ArgumentException("Contribution must be > 0.", nameof(contribution));
        if (current >= target) return 0;
        return (int)Math.Ceiling((double)(target - current) / (double)contribution);
    }

    public DateTime EstimateCompletionDate(DateTime startDate, ContributionFrequency frequency, int periodsNeeded)
    {
        return frequency switch
        {
            ContributionFrequency.Daily => startDate.AddDays(periodsNeeded),
            ContributionFrequency.Weekly => startDate.AddDays(periodsNeeded * 7),
            ContributionFrequency.BiWeekly => startDate.AddDays(periodsNeeded * 14),
            ContributionFrequency.Monthly => startDate.AddMonths(periodsNeeded),
            ContributionFrequency.BiMonthly => startDate.AddMonths(periodsNeeded * 2),
            ContributionFrequency.Quarterly => startDate.AddMonths(periodsNeeded * 3),
            ContributionFrequency.BiQuarterly => startDate.AddMonths(periodsNeeded * 6),
            ContributionFrequency.Annually => startDate.AddYears(periodsNeeded),
            _ => startDate.AddMonths(periodsNeeded)
        };
    }
}