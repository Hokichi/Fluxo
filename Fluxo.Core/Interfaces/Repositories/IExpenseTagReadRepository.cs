namespace Fluxo.Core.Interfaces.Repositories;

public interface IExpenseTagReadRepository<T> : IReadRepository<T> where T : class
{
    Task<IReadOnlyList<(T Tag, int Count)>> GetTagsByCountDescendingAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<(T Tag, int Count)>> GetTodayTagsByCountDescendingAsync(CancellationToken cancellationToken = default);
}
