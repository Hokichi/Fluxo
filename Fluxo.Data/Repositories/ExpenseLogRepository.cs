using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Data.Repositories;

public sealed class ExpenseLogRepository(FluxoDbContext dbContext)
    : Repository<ExpenseLog>(dbContext), IExpenseLogRepository
{
    public override async Task<IReadOnlyList<ExpenseLog>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await QueryWithNavigations().ToListAsync(cancellationToken);
    }

    public override async Task<ExpenseLog?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (FindTrackedEntity(id) is { } trackedExpenseLog)
            return trackedExpenseLog;

        return await QueryWithNavigations()
            .FirstOrDefaultAsync(log => log.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<ExpenseLog>> GetByDayAsync(DateTime day,
        CancellationToken cancellationToken = default)
    {
        var start = day.Date;
        var end = start.AddDays(1);
        return await QueryWithNavigations()
            .Where(log => log.DeductedOn >= start && log.DeductedOn < end)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExpenseLog>> GetByWeekAsync(DateTime startOfWeek, DateTime endOfWeek,
        CancellationToken cancellationToken = default)
    {
        var start = startOfWeek.Date;
        var end = endOfWeek.Date.AddDays(1);
        return await QueryWithNavigations()
            .Where(log => log.DeductedOn >= start && log.DeductedOn < end)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExpenseLog>> GetByMonthAsync(int month,
        CancellationToken cancellationToken = default)
    {
        return await QueryWithNavigations()
            .Where(log => log.DeductedOn.Month == month)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExpenseLog>> GetByCategoryAsync(ExpenseCategory category,
        CancellationToken cancellationToken = default)
    {
        return await QueryWithNavigations()
            .Where(log => log.Expense.ExpenseCategory == category)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExpenseLog>> GetBySpendingSourceIdAsync(int spendingSourceId,
        CancellationToken cancellationToken = default)
    {
        return await QueryWithNavigations()
            .Where(log => log.SpendingSourceId == spendingSourceId)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<ExpenseLog> QueryWithNavigations()
    {
        return DbSet
            .AsNoTrackingWithIdentityResolution()
            .Include(log => log.Expense)
            .ThenInclude(expense => expense.ExpenseTag)
            .Include(log => log.Expense)
            .ThenInclude(expense => expense.SpendingSource)
            .Include(log => log.SpendingSource);
    }
}
