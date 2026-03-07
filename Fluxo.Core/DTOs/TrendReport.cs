namespace Fluxo.Core.DTOs;

public sealed class TrendReport
{
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
    public DateTime From { get; init; }
    public DateTime To { get; init; }

    // ── Top-level totals ───────────────────────────────────────────────────────
    public decimal TotalIncome { get; init; }
    public decimal TotalExpenses { get; init; }
    public decimal TotalFixedExpenses { get; init; }
    public decimal TotalSaved { get; init; }
    public decimal NetCashFlow => TotalIncome - TotalExpenses - TotalFixedExpenses;

    // ── Bucket breakdown ──────────────────────────────────────────────────────
    public IReadOnlyList<BucketTrend> BucketTrends { get; init; } = [];

    // ── Tag breakdown — what the user spends most on ──────────────────────────
    public IReadOnlyList<TagSpendSummary> TopTagSpends { get; init; } = [];

    // ── Monthly averages ──────────────────────────────────────────────────────
    public decimal AverageMonthlyIncome { get; init; }
    public decimal AverageMonthlyExpenses { get; init; }

    /// <summary>
    /// Money that neither ended up in a savings account nor was spent.
    /// Positive = "sitting idle", negative = overspent income.
    /// </summary>
    public decimal IdleMoney { get; init; }

    // ── BNPL summary ──────────────────────────────────────────────────────────
    public decimal TotalBnplCharged { get; init; }
    public decimal TotalBnplSetAside { get; init; }

    // ── Month-over-month rows for sparklines ──────────────────────────────────
    public IReadOnlyList<MonthlySpendPoint> MonthlyBreakdown { get; init; } = [];
}