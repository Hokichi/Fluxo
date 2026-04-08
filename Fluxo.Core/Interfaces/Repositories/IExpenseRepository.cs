using Fluxo.Core.Entities;
using Fluxo.Core.Enums;

namespace Fluxo.Core.Interfaces.Repositories;

public interface IExpenseRepository : IRepository<Expense>
{
    Task<IReadOnlyList<Expense>> GetByDayAsync(DateTime day, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Expense>> GetByWeekAsync(DateTime startOfWeek, DateTime endOfWeek, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Expense>> GetByMonthAsync(int month, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Expense>> GetByKindAsync(ExpenseKind kind, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Expense>> GetByCategoryAsync(ExpenseCategory category, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Expense>> GetByTagIdAsync(int tagId, CancellationToken cancellationToken = default);
}
