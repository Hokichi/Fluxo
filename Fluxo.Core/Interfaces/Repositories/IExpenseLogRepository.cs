using Fluxo.Core.Entities;

namespace Fluxo.Core.Interfaces.Repositories;

public interface IExpenseLogRepository : IRepository<ExpenseLog>
{
    Task<IReadOnlyList<ExpenseLog>> GetBySpendingSourceIdAsync(int spendingSourceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExpenseLog>> GetTodayBySpendingSourceIdAsync(int spendingSourceId, CancellationToken cancellationToken = default);
}
