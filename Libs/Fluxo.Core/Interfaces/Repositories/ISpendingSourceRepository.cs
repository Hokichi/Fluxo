using Fluxo.Core.Entities;
using Fluxo.Core.Filters;

namespace Fluxo.Core.Interfaces.Repositories;

public interface ISpendingSourceRepository : IRepository<SpendingSource>
{
    Task<IReadOnlyList<SpendingSource>> SearchAsync(SpendingSourceFilter filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SpendingSource>> GetMarkedForDeletionAsync(CancellationToken cancellationToken = default);
}