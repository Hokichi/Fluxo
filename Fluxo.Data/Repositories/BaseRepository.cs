using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Data.Repositories;

public abstract class BaseRepository<T> : IRepository<T> where T : class
{
    protected readonly AppDbContext Db;

    protected BaseRepository(AppDbContext db) => Db = db;

    public virtual async Task<T?> GetByIdAsync(int id)
        => await Db.Set<T>().FindAsync(id);

    public virtual async Task<IReadOnlyList<T>> GetAllAsync()
        => await Db.Set<T>().ToListAsync();

    public virtual async Task AddAsync(T entity)
        => await Db.Set<T>().AddAsync(entity);

    public virtual Task UpdateAsync(T entity)
    {
        Db.Set<T>().Update(entity);
        return Task.CompletedTask;
    }

    public virtual async Task DeleteAsync(int id)
    {
        var entity = await Db.Set<T>().FindAsync(id);
        if (entity is not null) Db.Set<T>().Remove(entity);
    }

    public async Task<int> SaveChangesAsync()
        => await Db.SaveChangesAsync();
}