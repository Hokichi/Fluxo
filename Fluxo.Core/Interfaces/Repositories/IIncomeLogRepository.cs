using Fluxo.Core.Entities;

namespace Fluxo.Core.Interfaces.Repositories;

public interface IIncomeLogRepository : IRepository<IncomeLog>
{
    Task<IReadOnlyList<IncomeLog>> GetBySpendingSourceIdAsync(int spendingSourceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IncomeLog>> GetTodayBySpendingSourceIdAsync(int spendingSourceId, CancellationToken cancellationToken = default);
}
