using Fluxo.Core.Entities;

namespace Fluxo.Core.Interfaces.Repositories;

public interface IBudgetAllocationRepository
{
    Task<BudgetAllocation?> GetAsync(CancellationToken cancellationToken = default);
    Task AddAsync(BudgetAllocation entity, CancellationToken cancellationToken = default);
    void Update(BudgetAllocation entity);
}
