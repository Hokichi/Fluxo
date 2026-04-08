using Fluxo.Core.Entities;
using Fluxo.Core.Enums;

namespace Fluxo.Core.Interfaces.Repositories;

public interface IExpenseLogRepository : IRepository<ExpenseLog>
{
    Task<IReadOnlyList<ExpenseLog>> GetByDayAsync(DateTime day, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExpenseLog>> GetByWeekAsync(DateTime startOfWeek, DateTime endOfWeek, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExpenseLog>> GetByMonthAsync(int month, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExpenseLog>> GetByCategoryAsync(ExpenseCategory category, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExpenseLog>> GetBySpendingSourceIdAsync(int spendingSourceId, CancellationToken cancellationToken = default);
}
