using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Data.Repositories;

public class Repository<T>(FluxoDbContext dbContext) : IRepository<T> where T : class
{
    private static readonly System.Reflection.PropertyInfo? IdProperty = typeof(T).GetProperty("Id");

    protected FluxoDbContext DbContext { get; } = dbContext;
    protected DbSet<T> DbSet { get; } = dbContext.Set<T>();

    protected T? FindTrackedEntity(int id)
    {
        if (IdProperty?.PropertyType != typeof(int))
            return null;

        return DbSet.Local.FirstOrDefault(entity => IdProperty.GetValue(entity) is int entityId && entityId == id);
    }

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public virtual async Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (FindTrackedEntity(id) is { } trackedEntity)
            return trackedEntity;

        return await DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => EF.Property<int>(entity, "Id") == id, cancellationToken);
    }

    public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await DbSet.AddAsync(entity, cancellationToken);
    }

    public void Update(T entity)
    {
        DbSet.Update(entity);
    }

    public void Remove(T entity)
    {
        DbSet.Remove(entity);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return DbContext.SaveChangesAsync(cancellationToken);
    }
}
