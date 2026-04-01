namespace Fluxo.Core.Interfaces.Repositories;

public interface IIncomeLogReadRepository<T> : IReadRepository<T> where T : class
{
    Task<IReadOnlyList<T>> GetByDateAsync(DateTime date, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> GetBySpendingSourceIdAsync(int spendingSourceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> GetTodayBySpendingSourceIdAsync(int spendingSourceId, CancellationToken cancellationToken = default);
}
