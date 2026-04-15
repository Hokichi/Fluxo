using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Data.Repositories;

public sealed class UserSettingsRepository(FluxoDbContext dbContext) : IUserSettingsRepository
{
    private readonly FluxoDbContext _dbContext = dbContext;
    private readonly DbSet<UserSettings> _dbSet = dbContext.UserSettings;

    public async Task<IReadOnlyList<UserSettings>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .OrderBy(settings => settings.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<UserSettings?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(settings => settings.Name == name, cancellationToken);
    }

    public Task AddAsync(UserSettings entity, CancellationToken cancellationToken = default)
    {
        return _dbSet.AddAsync(entity, cancellationToken).AsTask();
    }

    public void Update(UserSettings entity)
    {
        var tracked = FindTrackedByName(entity.Name);
        if (tracked is not null)
        {
            _dbContext.Entry(tracked).CurrentValues.SetValues(entity);
            return;
        }

        _dbContext.Entry(entity).State = EntityState.Modified;
    }

    public void Remove(UserSettings entity)
    {
        var tracked = FindTrackedByName(entity.Name);
        if (tracked is not null)
        {
            _dbContext.Entry(tracked).State = EntityState.Deleted;
            return;
        }

        _dbContext.Entry(entity).State = EntityState.Deleted;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }

    private UserSettings? FindTrackedByName(string name)
    {
        return _dbSet.Local.FirstOrDefault(setting =>
            string.Equals(setting.Name, name, StringComparison.Ordinal));
    }
}
