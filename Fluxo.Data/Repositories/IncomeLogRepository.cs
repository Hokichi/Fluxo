using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Data.Repositories;

public sealed class IncomeLogRepository(FluxoDbContext dbContext)
    : Repository<IncomeLog>(dbContext), IIncomeLogRepository
{
    public override async Task<IReadOnlyList<IncomeLog>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await QueryWithNavigations().ToListAsync(cancellationToken);
    }

    public override async Task<IncomeLog?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await QueryWithNavigations()
            .FirstOrDefaultAsync(log => log.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<IncomeLog>> GetByDayAsync(DateTime day,
        CancellationToken cancellationToken = default)
    {
        var start = day.Date;
        var end = start.AddDays(1);
        return await QueryWithNavigations()
            .Where(log => log.AddedOn >= start && log.AddedOn < end)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<IncomeLog>> GetByWeekAsync(DateTime startOfWeek, DateTime endOfWeek,
        CancellationToken cancellationToken = default)
    {
        var start = startOfWeek.Date;
        var end = endOfWeek.Date.AddDays(1);
        return await QueryWithNavigations()
            .Where(log => log.AddedOn >= start && log.AddedOn < end)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<IncomeLog>> GetByMonthAsync(int month,
        CancellationToken cancellationToken = default)
    {
        return await QueryWithNavigations()
            .Where(log => log.AddedOn.Month == month)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<IncomeLog>> GetBySpendingSourceIdAsync(int spendingSourceId,
        CancellationToken cancellationToken = default)
    {
        return await QueryWithNavigations()
            .Where(log => EF.Property<int>(log, "SpendingSourceId") == spendingSourceId)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<IncomeLog> QueryWithNavigations()
    {
        return DbSet
            .AsNoTracking()
            .Include(log => log.SpendingSource);
    }
}
