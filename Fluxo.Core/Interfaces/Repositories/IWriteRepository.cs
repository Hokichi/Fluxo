using System.Threading;
using System.Threading.Tasks;

namespace Fluxo.Core.Interfaces.Repositories;

public interface IWriteRepository<T> where T : class
{
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    void Update(T entity);
    void Remove(T entity);
}
