using Fluxo.Core.Entities;

namespace Fluxo.Core.Interfaces.Repositories;

public interface IFixedExpenseHistoryRepository : IRepository<FixedExpenseHistory>
{
    Task<IReadOnlyList<FixedExpenseHistory>> GetByFixedExpenseAsync(int fixedExpenseId);

    Task<IReadOnlyList<FixedExpenseHistory>> GetByMonthAsync(int month, int year);

    Task<IReadOnlyList<FixedExpenseHistory>> GetByDateRangeAsync(DateTime from, DateTime to);

    /// <summary>Average paid amount over all history rows for a variable fixed expense.</summary>
    Task<decimal?> GetAverageAmountAsync(int fixedExpenseId);

    /// <summary>
    /// Total amount spent on fixed expenses in the given month.
    /// Used by BudgetService to compute actual spend for the Needs bucket.
    /// </summary>
    Task<decimal> GetTotalForMonthAsync(int month, int year);
}