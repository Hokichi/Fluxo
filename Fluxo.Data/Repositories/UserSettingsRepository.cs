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
        _dbSet.Update(entity);
    }

    public void Remove(UserSettings entity)
    {
        _dbSet.Remove(entity);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}