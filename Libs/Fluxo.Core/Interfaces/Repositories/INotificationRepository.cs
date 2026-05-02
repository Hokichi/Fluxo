using Fluxo.Core.Entities;

namespace Fluxo.Core.Interfaces.Repositories;

public interface INotificationRepository : IRepository<Notification>
{
    Task<Notification?> GetByTypeAsync(string type, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Notification>> GetActiveAsync(CancellationToken cancellationToken = default);
}
