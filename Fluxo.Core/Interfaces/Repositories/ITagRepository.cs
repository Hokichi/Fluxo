using Fluxo.Core.Entities;

namespace Fluxo.Core.Interfaces.Repositories;

public interface ITagRepository : IRepository<Tag>
{
    /// <summary>Returns how many expenses + fixed expenses currently carry this tag.</summary>
    Task<int> GetUsageCountAsync(int tagId);
}