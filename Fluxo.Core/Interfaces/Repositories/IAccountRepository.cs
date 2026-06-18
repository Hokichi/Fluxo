using Fluxo.Core.Entities;
using Fluxo.Core.Filters;

namespace Fluxo.Core.Interfaces.Repositories;

public interface IAccountRepository : IRepository<Account>
{
    Task<IReadOnlyList<Account>> SearchAsync(AccountFilter filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Account>> GetMarkedForDeletionAsync(CancellationToken cancellationToken = default);
}