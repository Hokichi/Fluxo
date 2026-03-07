using Fluxo.Core.Entities;

namespace Fluxo.Core.Interfaces.Repositories;

public interface IIncomeEntryRepository : IRepository<IncomeEntry>
{
    Task<IReadOnlyList<IncomeEntry>> GetByMonthAsync(int month, int year);

    Task<IReadOnlyList<IncomeEntry>> GetBySourceAsync(int sourceId);

    Task<IReadOnlyList<IncomeEntry>> GetByDateRangeAsync(DateTime from, DateTime to);

    /// <summary>
    /// Sum of all entry amounts for the given month, across all sources.
    /// Used by BudgetService and DashboardService as the base for 50/30/20.
    /// </summary>
    Task<decimal> GetTotalForMonthAsync(int month, int year);
}