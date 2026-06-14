using Fluxo.Core.Entities;
using Fluxo.Core.Filters;

namespace Fluxo.Core.Interfaces.Repositories;

public interface IIncomeLogRepository : IRepository<IncomeLog>
{
    Task<IReadOnlyList<IncomeLog>> SearchAsync(IncomeLogFilter filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IncomeLog>> GetMarkedForDeletionAsync(CancellationToken cancellationToken = default);
}
