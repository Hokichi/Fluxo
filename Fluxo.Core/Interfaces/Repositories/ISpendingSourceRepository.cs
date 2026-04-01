using Fluxo.Core.Entities;
using Fluxo.Core.Enums;

namespace Fluxo.Core.Interfaces.Repositories;

public interface ISpendingSourceRepository : IRepository<SpendingSource>
{
    Task<IReadOnlyList<SpendingSource>> GetByDateAsync(DateTime date, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SpendingSource>> GetBySourceTypeAsync(SpendingSourceType sourceType, CancellationToken cancellationToken = default);
}
