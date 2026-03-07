namespace Fluxo.Core.DTOs;

public sealed class DashboardSummary
{
    public int Month { get; init; }
    public int Year { get; init; }
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;

    // ── Income panel ──────────────────────────────────────────────────────────
    public decimal TotalIncome { get; init; }

    /// <summary>
    /// Per-source breakdown shown in the income panel.
    /// </summary>
    public IReadOnlyList<IncomeSourceSummary> IncomeSources { get; init; } = [];

    /// <summary>
    /// Grey overlay: total BNPL set-aside across all BNPL expenses this month.
    /// Displayed alongside TotalIncome so the user knows how much is "spoken for".
    /// </summary>
    public decimal BnplSetAsideTotal { get; init; }

    // ── 50/30/20 budget panel ─────────────────────────────────────────────────
    public MonthlyBudgetSummary Budget { get; init; } = new();

    // ── Fixed expenses panel ─────────────────────────────────────────────────
    public decimal TotalMonthlyFixedExpenses { get; init; }

    /// <summary>Fixed expenses that are due (or overdue) and haven't been paid yet.</summary>
    public IReadOnlyList<FixedExpenseDueSummary> PendingFixedExpenses { get; init; } = [];

    // ── Savings panel ────────────────────────────────────────────────────────
    public IReadOnlyList<SavingsAccountSnapshot> SavingsAccounts { get; init; } = [];

    public IReadOnlyList<GoalProgressSummary> SavingsGoals { get; init; } = [];

    // ── BNPL panel ───────────────────────────────────────────────────────────
    public IReadOnlyList<BnplSourceSnapshot> BnplSources { get; init; } = [];

    // ── Quick stats ──────────────────────────────────────────────────────────
    /// <summary>Income minus fixed expenses minus BNPL set-aside = truly disposable.</summary>
    public decimal DisposableIncome => TotalIncome - TotalMonthlyFixedExpenses - BnplSetAsideTotal;

    public decimal IdleMoney { get; init; }
}