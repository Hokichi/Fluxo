using Fluxo.Core.Entities;
using Fluxo.Core.Filters;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Data.Repositories;

public sealed class TransactionRepository(FluxoDbContext dbContext)
    : Repository<Transaction>(dbContext), ITransactionRepository
{
    public override async Task<IReadOnlyList<Transaction>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await Query().Where(transaction => !transaction.IsForDeletion)
            .OrderByDescending(transaction => transaction.OccurredOn)
            .ThenByDescending(transaction => transaction.LoggedOn)
            .ToListAsync(cancellationToken);

    public override async Task<Transaction?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        FindTrackedEntity(id) ?? await Query().FirstOrDefaultAsync(transaction => transaction.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Transaction>> SearchAsync(TransactionFilter filter, CancellationToken cancellationToken = default)
    {
        var query = Query();
        if (!filter.IncludeDeleted) query = query.Where(transaction => !transaction.IsForDeletion);
        if (filter.Type.HasValue) query = query.Where(transaction => transaction.Type == filter.Type);
        if (filter.AccountId.HasValue) query = query.Where(transaction => transaction.AccountId == filter.AccountId);
        if (filter.StartDate.HasValue) query = query.Where(transaction => transaction.OccurredOn >= filter.StartDate);
        if (filter.EndDate.HasValue) query = query.Where(transaction => transaction.OccurredOn <= filter.EndDate);
        if (filter.ExpenseCategory.HasValue) query = query.Where(transaction => transaction.ExpenseCategory == filter.ExpenseCategory);
        if (filter.TagId.HasValue) query = query.Where(transaction => transaction.TagId == filter.TagId);
        return await query
            .OrderByDescending(transaction => transaction.OccurredOn)
            .ThenByDescending(transaction => transaction.LoggedOn)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Transaction>> GetMarkedForDeletionAsync(CancellationToken cancellationToken = default) =>
        await Query().Where(transaction => transaction.IsForDeletion).ToListAsync(cancellationToken);

    private IQueryable<Transaction> Query() => DbSet
        .AsNoTrackingWithIdentityResolution()
        .Include(transaction => transaction.Account)
        .Include(transaction => transaction.Tag);
}
