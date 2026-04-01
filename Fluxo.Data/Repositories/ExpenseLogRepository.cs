using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Data.Repositories;

public sealed class ExpenseLogRepository(FluxoDbContext dbContext)
    : Repository<ExpenseLog>(dbContext), IExpenseLogRepository
{
    private IQueryable<ExpenseLog> QueryWithNavigations()
    {
        return DbSet
            .Include(log => log.Expense)
                .ThenInclude(expense => expense.ExpenseTag)
            .Include(log => log.Expense)
                .ThenInclude(expense => expense.SpendingSource)
            .Include(log => log.SpendingSource);
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

    public override async Task<IReadOnlyList<ExpenseLog>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await QueryWithNavigations().ToListAsync(cancellationToken);
    }

    public override async Task<ExpenseLog?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await QueryWithNavigations()
            .FirstOrDefaultAsync(log => log.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<ExpenseLog>> GetByDateAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var (start, end) = GetDayRange(date);
        return await QueryWithNavigations()
            .Where(log => log.DeductedOn >= start && log.DeductedOn < end)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExpenseLog>> GetByCategoryAsync(ExpenseCategory category, CancellationToken cancellationToken = default)
    {
        return await QueryWithNavigations()
            .Where(log => log.Expense.ExpenseCategory == category)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExpenseLog>> GetBySpendingSourceIdAsync(int spendingSourceId, CancellationToken cancellationToken = default)
    {
        return await QueryWithNavigations()
            .Where(log => EF.Property<int>(log, "SpendingSourceId") == spendingSourceId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExpenseLog>> GetTodayByCategoryAsync(ExpenseCategory category, CancellationToken cancellationToken = default)
    {
        var (start, end) = GetTodayRange();
        return await QueryWithNavigations()
            .Where(log => log.Expense.ExpenseCategory == category)
            .Where(log => log.DeductedOn >= start && log.DeductedOn < end)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExpenseLog>> GetTodayBySpendingSourceIdAsync(int spendingSourceId, CancellationToken cancellationToken = default)
    {
        var (start, end) = GetTodayRange();
        return await QueryWithNavigations()
            .Where(log => EF.Property<int>(log, "SpendingSourceId") == spendingSourceId)
            .Where(log => log.DeductedOn >= start && log.DeductedOn < end)
            .ToListAsync(cancellationToken);
    }
}
