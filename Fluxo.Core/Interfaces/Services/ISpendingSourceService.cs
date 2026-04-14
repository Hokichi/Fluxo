using Fluxo.Core.DTO;
using Fluxo.Core.Filters;

namespace Fluxo.Core.Interfaces.Services;

public interface ISpendingSourceService
{
    Task<IReadOnlyList<SpendingSourceDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SpendingSourceDto>> SearchAsync(SpendingSourceFilter filter, CancellationToken cancellationToken = default);
    Task AddAsync(SpendingSourceDto dto, CancellationToken cancellationToken = default);
    Task DeleteAsync(CancellationToken cancellationToken = default);
    Task AddIncomeAsync(int spendingSourceId, decimal amount, string notes, CancellationToken cancellationToken = default);
}
