using Fluxo.Core.Entities;

namespace Fluxo.Core.Interfaces.Repositories;

public interface ITagRepository : IRepository<Tag>
{
    Task<IReadOnlyList<(Tag Tag, int Count)>> GetTagsByCountDescendingAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<(Tag Tag, int Count)>> GetTodayTagsByCountDescendingAsync(
        CancellationToken cancellationToken = default);
}