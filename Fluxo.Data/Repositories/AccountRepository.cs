using Fluxo.Core.Entities;
using Fluxo.Core.Filters;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Data.Repositories;

public sealed class AccountRepository(FluxoDbContext dbContext)
    : Repository<Account>(dbContext), IAccountRepository
{
    public async Task<IReadOnlyList<Account>> SearchAsync(AccountFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Name))
            query = query.Where(s => s.Name.Contains(filter.Name));

        if (filter.Type.HasValue)
            query = query.Where(s => s.AccountType == filter.Type);

        if (filter.PinnedOnUIOnly)
            query = query.Where(s => s.PinnedOnUI);

        if (filter.EnabledOnly)
            query = query.Where(s => s.IsEnabled);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Account>> GetMarkedForDeletionAsync(
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(s => s.IsForDeletion)
            .ToListAsync(cancellationToken);
    }
}
