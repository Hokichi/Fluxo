using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Data.Repositories;

public sealed class BudgetAllocationRepository(FluxoDbContext dbContext) : IBudgetAllocationRepository
{
    private readonly FluxoDbContext _dbContext = dbContext;
    private readonly DbSet<BudgetAllocation> _dbSet = dbContext.BudgetAllocation;

    public Task<BudgetAllocation?> GetAsync(CancellationToken cancellationToken = default)
    {
        var local = _dbSet.Local.SingleOrDefault();
        if (local is not null)
            return Task.FromResult<BudgetAllocation?>(local);

        return _dbSet
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task AddAsync(BudgetAllocation entity, CancellationToken cancellationToken = default)
    {
        return _dbSet.AddAsync(entity, cancellationToken).AsTask();
    }

    public void Update(BudgetAllocation entity)
    {
        var tracked = _dbSet.Local.FirstOrDefault(allocation => allocation.Id == entity.Id);
        if (tracked is not null)
        {
            _dbContext.Entry(tracked).CurrentValues.SetValues(entity);
            return;
        }

        _dbContext.Entry(entity).State = EntityState.Modified;
    }
}
