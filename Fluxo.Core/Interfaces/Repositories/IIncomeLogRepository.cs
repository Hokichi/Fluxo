using Fluxo.Core.Entities;

namespace Fluxo.Core.Interfaces.Repositories;

public interface IIncomeLogRepository : IRepository<IncomeLog>
{
    Task<IReadOnlyList<IncomeLog>> GetByDayAsync(DateTime day, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IncomeLog>> GetByWeekAsync(DateTime startOfWeek, DateTime endOfWeek, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IncomeLog>> GetByMonthAsync(int month, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IncomeLog>> GetBySpendingSourceIdAsync(int spendingSourceId, CancellationToken cancellationToken = default);
}
