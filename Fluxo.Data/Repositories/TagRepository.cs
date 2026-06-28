using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Data.Repositories;

public sealed class TagRepository(FluxoDbContext dbContext)
    : Repository<Tag>(dbContext), ITagRepository
{
    public Task<IReadOnlyList<(Tag Tag, int Count)>> GetTagsByCountDescendingAsync(
        CancellationToken cancellationToken = default) =>
        GetTagsByCountDescendingAsync(null, null, cancellationToken);

    public Task<IReadOnlyList<(Tag Tag, int Count)>> GetTodayTagsByCountDescendingAsync(
        CancellationToken cancellationToken = default) =>
        GetTagsByCountDescendingAsync(DateTime.Today, DateTime.Today.AddDays(1), cancellationToken);

    private async Task<IReadOnlyList<(Tag Tag, int Count)>> GetTagsByCountDescendingAsync(
        DateTime? start,
        DateTime? end,
        CancellationToken cancellationToken)
    {
        var query = DbContext.Transactions
            .Where(transaction => transaction.TagId.HasValue && !transaction.IsForDeletion);
        if (start.HasValue) query = query.Where(transaction => transaction.OccurredOn >= start.Value);
        if (end.HasValue) query = query.Where(transaction => transaction.OccurredOn < end.Value);

        var countsByTagId = await query
            .GroupBy(transaction => transaction.TagId!.Value)
            .Select(group => new { TagId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.TagId, item => item.Count, cancellationToken);
        var tags = await DbSet.AsNoTracking().ToListAsync(cancellationToken);
        return tags
            .Select(tag => (Tag: tag, Count: countsByTagId.GetValueOrDefault(tag.Id)))
            .OrderByDescending(item => item.Count)
            .ToList();
    }
}
