using System.Reflection;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Data.Repositories;

public class Repository<T>(FluxoDbContext dbContext) : IRepository<T> where T : class
{
    private static readonly PropertyInfo? IdProperty = typeof(T).GetProperty("Id");

    protected FluxoDbContext DbContext { get; } = dbContext;
    protected DbSet<T> DbSet { get; } = dbContext.Set<T>();

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
        // If we're already tracking a different instance with the same key,
        // copy values into the tracked instance instead of attaching a duplicate.
        if (IdProperty?.GetValue(entity) is int id)
        {
            var tracked = FindTrackedEntity(id);
            if (tracked != null)
            {
                DbContext.Entry(tracked).CurrentValues.SetValues(entity);
                return;
            }
        }

        // Use Entry().State instead of DbSet.Update() to avoid recursively attaching
        // navigation properties, which can cause duplicate tracking conflicts.
        DbContext.Entry(entity).State = EntityState.Modified;
    }

    public void Remove(T entity)
    {
        // If we're already tracking a different instance with the same key,
        // mark the tracked instance as deleted instead of attaching a duplicate.
        if (IdProperty?.GetValue(entity) is int id)
        {
            var tracked = FindTrackedEntity(id);
            if (tracked != null)
            {
                DbContext.Entry(tracked).State = EntityState.Deleted;
                return;
            }
        }

        // Use Entry().State instead of DbSet.Remove() to avoid recursively attaching
        // navigation properties, which can cause duplicate tracking conflicts.
        DbContext.Entry(entity).State = EntityState.Deleted;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return DbContext.SaveChangesAsync(cancellationToken);
    }

    protected T? FindTrackedEntity(int id)
    {
        if (IdProperty?.PropertyType != typeof(int))
            return null;

        return DbSet.Local.FirstOrDefault(entity => IdProperty.GetValue(entity) is int entityId && entityId == id);
    }
}