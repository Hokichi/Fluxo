using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Data.Repositories;

public sealed class SpendingSourceRepository(FluxoDbContext dbContext)
    : Repository<SpendingSource>(dbContext), ISpendingSourceRepository
{
    public async Task<IReadOnlyList<SpendingSource>> GetByDateAsync(DateTime date,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = GetDayRange(date);
        return await DbSet
            .Where(source => source.DueDate >= start && source.DueDate < end)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SpendingSource>> GetBySourceTypeAsync(SpendingSourceType sourceType,
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(source => source.SpendingSourceType == sourceType)
            .ToListAsync(cancellationToken);
    }

    private static (DateTime Start, DateTime End) GetDayRange(DateTime date)
    {
        var start = date.Date;
        return (start, start.AddDays(1));
    }
}