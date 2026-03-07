using Fluxo.Core.DTOs;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;

namespace Fluxo.Core.Interfaces.Services;

public interface IBudgetService
{
    /// <summary>
    /// Returns the full 50/30/20 breakdown for the month — allocations,
    /// actual spend, remaining, and idle money.
    /// Uses the BudgetConfig for that month/year or falls back to 50/30/20.
    /// </summary>
    Task<MonthlyBudgetSummary> GetSummaryAsync(int month, int year);

    /// <summary>Persisted split percentages for a specific month.</summary>
    Task<BudgetConfig?> GetConfigAsync(int month, int year);

    /// <summary>
    /// Create or update the percentage split for a month.
    /// Validates that Needs + Wants + Savings == 100 before saving.
    /// </summary>
    Task<BudgetConfig> UpsertConfigAsync(int month, int year,
        decimal needsPct, decimal wantsPct, decimal savingsPct);

    /// <summary>
    /// Amount spent in a specific category this month (variable + fixed expenses combined).
    /// </summary>
    Task<decimal> GetSpentInCategoryAsync(ExpenseCategory category, int month, int year);

    /// <summary>
    /// Money that is neither allocated to a bucket nor spent — "sitting doing nothing".
    /// Surfaced in the dashboard as an actionable nudge.
    /// </summary>
    Task<decimal> GetIdleMoneyAsync(int month, int year);
}