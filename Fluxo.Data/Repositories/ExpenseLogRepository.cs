using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Data.Repositories;

public sealed class ExpenseLogRepository(FluxoDbContext dbContext)
    : Repository<ExpenseLog>(dbContext), IExpenseLogRepository
{
    private static (DateTime Start, DateTime End) GetTodayRange()
    {
        var start = DateTime.Today;
        return (start, start.AddDays(1));
    }

    public async Task<IReadOnlyList<ExpenseLog>> GetByCategoryAsync(ExpenseCategory category, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(log => log.Expense.ExpenseCategory == category)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExpenseLog>> GetBySpendingSourceIdAsync(int spendingSourceId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(log => EF.Property<int>(log, "SpendingSourceId") == spendingSourceId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExpenseLog>> GetTodayByCategoryAsync(ExpenseCategory category, CancellationToken cancellationToken = default)
    {
        var (start, end) = GetTodayRange();
        return await DbSet
            .Where(log => log.Expense.ExpenseCategory == category)
            .Where(log => log.DeductedOn >= start && log.DeductedOn < end)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExpenseLog>> GetTodayBySpendingSourceIdAsync(int spendingSourceId, CancellationToken cancellationToken = default)
    {
        var (start, end) = GetTodayRange();
        return await DbSet
            .Where(log => EF.Property<int>(log, "SpendingSourceId") == spendingSourceId)
            .Where(log => log.DeductedOn >= start && log.DeductedOn < end)
            .ToListAsync(cancellationToken);
    }
}
