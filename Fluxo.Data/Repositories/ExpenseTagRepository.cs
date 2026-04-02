using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Data.Repositories;

public sealed class ExpenseTagRepository(FluxoDbContext dbContext)
    : Repository<ExpenseTag>(dbContext), IExpenseTagRepository
{
    private static (DateTime Start, DateTime End) GetDayRange(DateTime date)
    {
        var start = date.Date;
        return (start, start.AddDays(1));
    }

    public async Task<IReadOnlyList<(ExpenseTag Tag, int Count)>> GetTagsByCountDescendingAsync(CancellationToken cancellationToken = default)
    {
        var countsByTagId = await DbContext.Expenses
            .GroupBy(expense => EF.Property<int>(expense, "ExpenseTagId"))
            .Select(group => new { TagId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.TagId, item => item.Count, cancellationToken);

        var tags = await DbSet.ToListAsync(cancellationToken);
        return tags
            .Select(tag => (Tag: tag, Count: countsByTagId.GetValueOrDefault(tag.Id)))
            .OrderByDescending(item => item.Count)
            .ToList();
    }

    public async Task<IReadOnlyList<(ExpenseTag Tag, int Count)>> GetTodayTagsByCountDescendingAsync(CancellationToken cancellationToken = default)
    {
        var (start, end) = GetDayRange(DateTime.Today);
        var countsByTagId = await DbContext.Expenses
            .Where(expense => expense.RecurringDate >= start && expense.RecurringDate < end)
            .GroupBy(expense => EF.Property<int>(expense, "ExpenseTagId"))
            .Select(group => new { TagId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.TagId, item => item.Count, cancellationToken);

        var tags = await DbSet.ToListAsync(cancellationToken);
        return tags
            .Select(tag => (Tag: tag, Count: countsByTagId.GetValueOrDefault(tag.Id)))
            .OrderByDescending(item => item.Count)
            .ToList();
    }
}
