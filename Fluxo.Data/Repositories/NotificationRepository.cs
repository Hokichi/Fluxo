using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Data.Repositories;

public sealed class NotificationRepository(FluxoDbContext dbContext)
    : Repository<Notification>(dbContext), INotificationRepository
{
    public async Task<Notification?> GetByTypeAsync(string type, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(notification => notification.Type == type && !notification.IsForDeletion)
            .OrderByDescending(notification => notification.CreatedOn)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Notification>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(notification => !notification.IsForDeletion)
            .OrderByDescending(notification => notification.CreatedOn)
            .ToListAsync(cancellationToken);
    }
}
