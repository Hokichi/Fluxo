using Fluxo.Core.Enums;

namespace Fluxo.Core.Interfaces.Repositories;

public interface IExpenseReadRepository<T> : IReadRepository<T> where T : class
{
    Task<IReadOnlyList<T>> GetByDayAsync(DateTime day, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> GetByWeekAsync(DateTime startOfWeek, DateTime endOfWeek, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> GetByMonthAsync(int month, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> GetByKindAsync(ExpenseKind kind, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> GetByCategoryAsync(ExpenseCategory category, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> GetByTagIdAsync(int tagId, CancellationToken cancellationToken = default);
}
