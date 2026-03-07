using Fluxo.Core.Entities;

namespace Fluxo.Core.Interfaces.Services;

public interface ITagService
{
    Task<IReadOnlyList<Tag>> GetAllAsync();

    Task<Tag> AddAsync(string name, string color = "#808080");

    Task<Tag> UpdateAsync(int tagId, string name, string color);

    /// <summary>
    /// Deletes a tag only if it has no current usages (expenses / fixed expenses).
    /// Throws InvalidOperationException if still in use.
    /// </summary>
    Task DeleteAsync(int tagId);

    /// <summary>
    /// Returns how many expenses and fixed expenses carry a given tag.
    /// Used by the UI to warn before deletion.
    /// </summary>
    Task<int> GetUsageCountAsync(int tagId);
}