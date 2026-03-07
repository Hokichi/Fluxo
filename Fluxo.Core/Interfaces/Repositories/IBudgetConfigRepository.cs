using Fluxo.Core.Entities;

namespace Fluxo.Core.Interfaces.Repositories;

public interface IBudgetConfigRepository : IRepository<BudgetConfig>
{
    /// <summary>
    /// Returns the config for the given month/year, or null if none exists.
    /// Callers should treat null as "use defaults (50/30/20)".
    /// </summary>
    Task<BudgetConfig?> GetByMonthAsync(int month, int year);

    /// <summary>
    /// Insert if no row exists for (Month, Year), otherwise update.
    /// Enforces the DB unique index on (Month, Year).
    /// </summary>
    Task UpsertAsync(BudgetConfig config);
}