using Fluxo.Core.Entities;

namespace Fluxo.Core.Interfaces.Repositories;

public interface IExpenseTagRepository : IRepository<ExpenseTag>
{
    Task<IReadOnlyList<(ExpenseTag Tag, int Count)>> GetTagsByCountDescendingAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<(ExpenseTag Tag, int Count)>> GetTodayTagsByCountDescendingAsync(
        CancellationToken cancellationToken = default);
}