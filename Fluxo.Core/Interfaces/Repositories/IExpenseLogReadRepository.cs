namespace Fluxo.Core.Interfaces.Repositories;

public interface IExpenseLogReadRepository<T> : IReadRepository<T> where T : class
{
    Task<IReadOnlyList<T>> GetBySpendingSourceIdAsync(int spendingSourceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> GetTodayBySpendingSourceIdAsync(int spendingSourceId, CancellationToken cancellationToken = default);
}
