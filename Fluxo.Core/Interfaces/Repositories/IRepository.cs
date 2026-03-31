using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Fluxo.Core.Interfaces.Repositories;

public interface IRepository<T> where T : class
{
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    void Update(T entity);
    void Remove(T entity);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
