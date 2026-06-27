using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Data.Repositories;

public sealed class ExpenseLogRepository(FluxoDbContext dbContext)
    : Repository<ExpenseLog>(dbContext), IExpenseLogRepository
{
    public override async Task<IReadOnlyList<ExpenseLog>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await QueryWithNavigations()
            .Where(log => !log.IsForDeletion)
            .ToListAsync(cancellationToken);
    }

    public async Task<ExpenseLog?> GetByLogIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (FindTrackedEntity(id) is { } tracked)
            return tracked;

        return await QueryWithNavigations()
            .FirstOrDefaultAsync(log => log.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<ExpenseLog>> GetByExpenseIdAsync(int expenseId,
        CancellationToken cancellationToken = default)
    {
        return await QueryWithNavigations()
            .Where(log => log.ExpenseId == expenseId && !log.IsForDeletion)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExpenseLog>> GetMarkedForDeletionAsync(
        CancellationToken cancellationToken = default)
    {
        return await QueryWithNavigations()
            .Where(log => log.IsForDeletion)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<ExpenseLog> QueryWithNavigations()
    {
        return DbSet
            .AsNoTrackingWithIdentityResolution()
            .Include(log => log.Expense)
            .ThenInclude(e => e.Tag)
            .Include(log => log.Expense)
            .ThenInclude(e => e.Account)
            .Include(log => log.Account);
    }
}
