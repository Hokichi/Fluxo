using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Data.Repositories;

public sealed class IncomeLogRepository(FluxoDbContext dbContext)
    : Repository<IncomeLog>(dbContext), IIncomeLogRepository
{
    private static (DateTime Start, DateTime End) GetTodayRange()
    {
        var start = DateTime.Today;
        return (start, start.AddDays(1));
    }

    public async Task<IReadOnlyList<IncomeLog>> GetBySpendingSourceIdAsync(int spendingSourceId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(log => EF.Property<int>(log, "SpendingSourceId") == spendingSourceId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<IncomeLog>> GetTodayBySpendingSourceIdAsync(int spendingSourceId, CancellationToken cancellationToken = default)
    {
        var (start, end) = GetTodayRange();
        return await DbSet
            .Where(log => EF.Property<int>(log, "SpendingSourceId") == spendingSourceId)
            .Where(log => log.AddedOn >= start && log.AddedOn < end)
            .ToListAsync(cancellationToken);
    }
}
