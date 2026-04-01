using Fluxo.Core.Entities;
using Fluxo.Core.Enums;

namespace Fluxo.Core.Interfaces.Repositories;

public interface IExpenseRepository : IRepository<Expense>
{
    Task<IReadOnlyList<Expense>> GetByDateAsync(DateTime date, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Expense>> GetByKindAsync(ExpenseKind kind, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Expense>> GetByCategoryAsync(ExpenseCategory category, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Expense>> GetByTagIdAsync(int tagId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Expense>> GetTodayByCategoryAsync(ExpenseCategory category, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Expense>> GetTodayByTagIdAsync(int tagId, CancellationToken cancellationToken = default);
}
