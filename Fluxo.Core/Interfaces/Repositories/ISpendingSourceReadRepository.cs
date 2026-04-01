using Fluxo.Core.Enums;

namespace Fluxo.Core.Interfaces.Repositories;

public interface ISpendingSourceReadRepository<T> : IReadRepository<T> where T : class
{
    Task<IReadOnlyList<T>> GetBySourceTypeAsync(SpendingSourceType sourceType, CancellationToken cancellationToken = default);
}
