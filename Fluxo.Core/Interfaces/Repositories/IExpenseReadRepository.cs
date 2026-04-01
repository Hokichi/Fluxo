using Fluxo.Core.Enums;

namespace Fluxo.Core.Interfaces.Repositories;

public interface IExpenseReadRepository<T> : IReadRepository<T> where T : class
{
    Task<IReadOnlyList<T>> GetByKindAsync(ExpenseKind kind, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> GetByCategoryAsync(ExpenseCategory category, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> GetByTagIdAsync(int tagId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> GetTodayByCategoryAsync(ExpenseCategory category, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> GetTodayByTagIdAsync(int tagId, CancellationToken cancellationToken = default);
}
