using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Data.Repositories;

public sealed class ExpenseRepository(FluxoDbContext dbContext)
    : Repository<Expense>(dbContext), IExpenseRepository
{
    private IQueryable<Expense> QueryWithNavigations()
    {
        return DbSet
            .Include(expense => expense.ExpenseTag)
            .Include(expense => expense.SpendingSource);
    }

    private static (DateTime Start, DateTime End) GetTodayRange()
    {
        var start = DateTime.Today;
        return (start, start.AddDays(1));
    }

    public override async Task<IReadOnlyList<Expense>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await QueryWithNavigations().ToListAsync(cancellationToken);
    }

    public override async Task<Expense?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await QueryWithNavigations()
            .FirstOrDefaultAsync(expense => expense.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Expense>> GetByKindAsync(ExpenseKind kind, CancellationToken cancellationToken = default)
    {
        return await QueryWithNavigations()
            .Where(expense => expense.ExpenseKind == kind)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Expense>> GetByCategoryAsync(ExpenseCategory category, CancellationToken cancellationToken = default)
    {
        return await QueryWithNavigations()
            .Where(expense => expense.ExpenseCategory == category)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Expense>> GetByTagIdAsync(int tagId, CancellationToken cancellationToken = default)
    {
        return await QueryWithNavigations()
            .Where(expense => EF.Property<int>(expense, "ExpenseTagId") == tagId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Expense>> GetTodayByCategoryAsync(ExpenseCategory category, CancellationToken cancellationToken = default)
    {
        var (start, end) = GetTodayRange();
        return await QueryWithNavigations()
            .Where(expense => expense.ExpenseCategory == category)
            .Where(expense => expense.RecurringDate >= start && expense.RecurringDate < end)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Expense>> GetTodayByTagIdAsync(int tagId, CancellationToken cancellationToken = default)
    {
        var (start, end) = GetTodayRange();
        return await QueryWithNavigations()
            .Where(expense => EF.Property<int>(expense, "ExpenseTagId") == tagId)
            .Where(expense => expense.RecurringDate >= start && expense.RecurringDate < end)
            .ToListAsync(cancellationToken);
    }
}
