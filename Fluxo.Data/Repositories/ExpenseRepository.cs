using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Data.Repositories;

public sealed class ExpenseRepository(FluxoDbContext dbContext)
    : Repository<Expense>(dbContext), IExpenseRepository
{
    public override async Task<IReadOnlyList<Expense>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await QueryWithNavigations().ToListAsync(cancellationToken);
    }

    public override async Task<Expense?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (FindTrackedEntity(id) is { } trackedExpense)
            return trackedExpense;

        return await QueryWithNavigations()
            .FirstOrDefaultAsync(expense => expense.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Expense>> GetByDayAsync(DateTime day, CancellationToken cancellationToken = default)
    {
        var start = day.Date;
        var end = start.AddDays(1);
        return await QueryWithNavigations()
            .Where(expense => expense.RecurringDate >= start && expense.RecurringDate < end)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Expense>> GetByWeekAsync(DateTime startOfWeek, DateTime endOfWeek,
        CancellationToken cancellationToken = default)
    {
        var start = startOfWeek.Date;
        var end = endOfWeek.Date.AddDays(1);
        return await QueryWithNavigations()
            .Where(expense => expense.RecurringDate >= start && expense.RecurringDate < end)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Expense>> GetByMonthAsync(int month, CancellationToken cancellationToken = default)
    {
        return await QueryWithNavigations()
            .Where(expense => expense.RecurringDate != null && expense.RecurringDate.Value.Month == month)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Expense>> GetByKindAsync(ExpenseKind kind,
        CancellationToken cancellationToken = default)
    {
        return await QueryWithNavigations()
            .Where(expense => expense.ExpenseKind == kind)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Expense>> GetByCategoryAsync(ExpenseCategory category,
        CancellationToken cancellationToken = default)
    {
        return await QueryWithNavigations()
            .Where(expense => expense.ExpenseCategory == category)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Expense>> GetByTagIdAsync(int tagId, CancellationToken cancellationToken = default)
    {
        return await QueryWithNavigations()
            .Where(expense => expense.ExpenseTagId == tagId)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<Expense> QueryWithNavigations()
    {
        return DbSet
            .AsNoTrackingWithIdentityResolution()
            .Include(expense => expense.ExpenseTag)
            .Include(expense => expense.SpendingSource);
    }
}