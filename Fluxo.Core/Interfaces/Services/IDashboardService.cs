using Fluxo.Core.DTOs;

namespace Fluxo.Core.Interfaces.Services;

public interface IDashboardService
{
    /// <summary>
    /// Builds the full DashboardSummary for the given month/year.
    /// Defaults to the current calendar month when parameters are null.
    /// </summary>
    Task<DashboardSummary> GetSummaryAsync(int? month = null, int? year = null);

    /// <summary>
    /// Lightweight refresh that only recalculates changed parts (e.g. after
    /// adding one expense without rebuilding the entire summary).
    /// Returns a fresh DashboardSummary.
    /// </summary>
    Task<DashboardSummary> RefreshAsync(DashboardSummary current);
}