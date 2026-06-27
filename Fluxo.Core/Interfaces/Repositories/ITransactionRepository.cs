using Fluxo.Core.Entities;
using Fluxo.Core.Filters;

namespace Fluxo.Core.Interfaces.Repositories;

public interface ITransactionRepository : IRepository<Transaction>
{
    Task<IReadOnlyList<Transaction>> SearchAsync(TransactionFilter filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Transaction>> GetMarkedForDeletionAsync(CancellationToken cancellationToken = default);
}
