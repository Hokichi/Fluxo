using Fluxo.Core.Entities;

namespace Fluxo.Core.Interfaces.Repositories;

public interface IUserSettingsRepository
{
    Task<IReadOnlyList<UserSettings>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<UserSettings?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task AddAsync(UserSettings entity, CancellationToken cancellationToken = default);
    void Update(UserSettings entity);
    void Remove(UserSettings entity);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}