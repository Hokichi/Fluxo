using Fluxo.Core.DTOs;

namespace Fluxo.Core.Interfaces.Services;

public interface ITrendService
{
    /// <summary>Full report for a single calendar month.</summary>
    Task<TrendReport> GetMonthlyReportAsync(int month, int year);

    /// <summary>
    /// Full report across a custom date range.
    /// Aggregates multiple months for averages and sparklines.
    /// </summary>
    Task<TrendReport> GetDateRangeReportAsync(DateTime from, DateTime to);

    /// <summary>
    /// Convenience: rolling N-month report ending today.
    /// Default = 6 months.
    /// </summary>
    Task<TrendReport> GetRollingReportAsync(int pastMonths = 6);

    /// <summary>
    /// Money that is in no savings account, no BNPL liability, and not
    /// recorded as spent — true "idle" money the user could put to work.
    /// </summary>
    Task<decimal> GetIdleMoneyAsync(int month, int year);
}