using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Data.Repositories;

public sealed class IncomeLogRepository(FluxoDbContext dbContext)
    : Repository<IncomeLog>(dbContext), IIncomeLogRepository
{
    private IQueryable<IncomeLog> QueryWithNavigations()
    {
        return DbSet.Include(log => log.SpendingSource);
    }

    private static (DateTime Start, DateTime End) GetTodayRange()
    {
        var start = DateTime.Today;
        return (start, start.AddDays(1));
    }

    private static (DateTime Start, DateTime End) GetDayRange(DateTime date)
    {
        var start = date.Date;
        return (start, start.AddDays(1));
    }

    public override async Task<IReadOnlyList<IncomeLog>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await QueryWithNavigations().ToListAsync(cancellationToken);
    }

    public override async Task<IncomeLog?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await QueryWithNavigations()
            .FirstOrDefaultAsync(log => log.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<IncomeLog>> GetByDateAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var (start, end) = GetDayRange(date);
        return await QueryWithNavigations()
            .Where(log => log.AddedOn >= start && log.AddedOn < end)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<IncomeLog>> GetBySpendingSourceIdAsync(int spendingSourceId, CancellationToken cancellationToken = default)
    {
        return await QueryWithNavigations()
            .Where(log => EF.Property<int>(log, "SpendingSourceId") == spendingSourceId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<IncomeLog>> GetTodayBySpendingSourceIdAsync(int spendingSourceId, CancellationToken cancellationToken = default)
    {
        var (start, end) = GetTodayRange();
        return await QueryWithNavigations()
            .Where(log => EF.Property<int>(log, "SpendingSourceId") == spendingSourceId)
            .Where(log => log.AddedOn >= start && log.AddedOn < end)
            .ToListAsync(cancellationToken);
    }
}
