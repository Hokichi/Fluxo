using Fluxo.Core.Entities;
using Fluxo.Core.Filters;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Data.Repositories;

public sealed class IncomeLogRepository(FluxoDbContext dbContext)
    : Repository<IncomeLog>(dbContext), IIncomeLogRepository
{
    public override async Task<IReadOnlyList<IncomeLog>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await QueryWithNavigations()
            .Where(log => !log.IsForDeletion)
            .ToListAsync(cancellationToken);
    }

    public override async Task<IncomeLog?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (FindTrackedEntity(id) is { } tracked)
            return tracked;

        return await QueryWithNavigations()
            .FirstOrDefaultAsync(log => log.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<IncomeLog>> SearchAsync(IncomeLogFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = QueryWithNavigations()
            .Where(log => !log.IsForDeletion);

        if (filter.Account is not null)
            query = query.Where(log => log.AccountId == filter.Account.Id);

        if (filter.StartDate.HasValue)
            query = query.Where(log => log.AddedOn >= filter.StartDate);

        if (filter.EndDate.HasValue)
            query = query.Where(log => log.AddedOn <= filter.EndDate);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<IncomeLog>> GetMarkedForDeletionAsync(
        CancellationToken cancellationToken = default)
    {
        return await QueryWithNavigations()
            .Where(log => log.IsForDeletion)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<IncomeLog> QueryWithNavigations()
    {
        return DbSet
            .AsNoTrackingWithIdentityResolution()
            .Include(log => log.Account);
    }
}
