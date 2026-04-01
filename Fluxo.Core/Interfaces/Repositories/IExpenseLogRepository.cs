using Fluxo.Core.Entities;
using Fluxo.Core.Enums;

namespace Fluxo.Core.Interfaces.Repositories;

public interface IExpenseLogRepository : IRepository<ExpenseLog>
{
    Task<IReadOnlyList<ExpenseLog>> GetByCategoryAsync(ExpenseCategory category, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExpenseLog>> GetBySpendingSourceIdAsync(int spendingSourceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExpenseLog>> GetTodayByCategoryAsync(ExpenseCategory category, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExpenseLog>> GetTodayBySpendingSourceIdAsync(int spendingSourceId, CancellationToken cancellationToken = default);
}
