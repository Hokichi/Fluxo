using Fluxo.Core.Entities;
using Fluxo.Core.Filters;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Data.Repositories;

public sealed class SpendingSourceRepository(FluxoDbContext dbContext)
    : Repository<SpendingSource>(dbContext), ISpendingSourceRepository
{
    public async Task<IReadOnlyList<SpendingSource>> SearchAsync(SpendingSourceFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Name))
            query = query.Where(s => s.Name.Contains(filter.Name));

        if (filter.Type.HasValue)
            query = query.Where(s => s.SpendingSourceType == filter.Type);

        if (filter.PinnedOnUIOnly)
            query = query.Where(s => s.PinnedOnUI);

        if (filter.EnabledOnly)
            query = query.Where(s => s.IsEnabled);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SpendingSource>> GetMarkedForDeletionAsync(
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(s => s.IsForDeletion)
            .ToListAsync(cancellationToken);
    }
}
