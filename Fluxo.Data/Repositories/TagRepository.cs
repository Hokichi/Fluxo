using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Data.Repositories;

public sealed class TagRepository(FluxoDbContext dbContext)
    : Repository<Tag>(dbContext), ITagRepository
{
    public async Task<IReadOnlyList<(Tag Tag, int Count)>> GetTagsByCountDescendingAsync(
        CancellationToken cancellationToken = default)
    {
        var countsByTagId = await DbContext.Expenses
            .GroupBy(expense => EF.Property<int>(expense, "TagId"))
            .Select(group => new { TagId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.TagId, item => item.Count, cancellationToken);

        var tags = await DbSet
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return tags
            .Select(tag => (Tag: tag, Count: countsByTagId.GetValueOrDefault(tag.Id)))
            .OrderByDescending(item => item.Count)
            .ToList();
    }

    public async Task<IReadOnlyList<(Tag Tag, int Count)>> GetTodayTagsByCountDescendingAsync(
        CancellationToken cancellationToken = default)
    {
        var countsByTagId = await DbContext.Expenses
            .GroupBy(expense => EF.Property<int>(expense, "TagId"))
            .Select(group => new { TagId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.TagId, item => item.Count, cancellationToken);

        var tags = await DbSet
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return tags
            .Select(tag => (Tag: tag, Count: countsByTagId.GetValueOrDefault(tag.Id)))
            .OrderByDescending(item => item.Count)
            .ToList();
    }
}
