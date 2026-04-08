namespace Fluxo.Core.Interfaces.Repositories;

public interface IIncomeLogReadRepository<T> : IReadRepository<T> where T : class
{
    Task<IReadOnlyList<T>> GetByDayAsync(DateTime day, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> GetByWeekAsync(DateTime startOfWeek, DateTime endOfWeek,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> GetByMonthAsync(int month, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> GetBySpendingSourceIdAsync(int spendingSourceId,
        CancellationToken cancellationToken = default);
}