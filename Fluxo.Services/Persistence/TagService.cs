using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Core.Interfaces.Services;

namespace Fluxo.Services.Persistence;

public sealed class TagService : ITagService
{
    private readonly ITagRepository _tags;

    public TagService(ITagRepository tags)
    {
        _tags = tags;
    }

    public Task<IReadOnlyList<Tag>> GetAllAsync()
    {
        return _tags.GetAllAsync();
    }

    public async Task<Tag> AddAsync(string name, string color = "#808080")
    {
        var tag = new Tag
        {
            Name = name,
            Color = color,
            CreatedAt = DateTime.UtcNow
        };
        await _tags.AddAsync(tag);
        await _tags.SaveChangesAsync();
        return tag;
    }

    public async Task<Tag> UpdateAsync(int tagId, string name, string color)
    {
        var tag = await _tags.GetByIdAsync(tagId)
                  ?? throw new InvalidOperationException($"Tag {tagId} not found.");
        tag.Name = name;
        tag.Color = color;
        await _tags.UpdateAsync(tag);
        await _tags.SaveChangesAsync();
        return tag;
    }

    public async Task DeleteAsync(int tagId)
    {
        var usage = await _tags.GetUsageCountAsync(tagId);
        if (usage > 0)
            throw new InvalidOperationException(
                $"Cannot delete tag — it is still used by {usage} expense(s). Remove it from those expenses first.");

        await _tags.DeleteAsync(tagId);
        await _tags.SaveChangesAsync();
    }

    public Task<int> GetUsageCountAsync(int tagId)
    {
        return _tags.GetUsageCountAsync(tagId);
    }
}