using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Data.Repositories;

public sealed class RecurringTransactionRepository(FluxoDbContext dbContext)
    : Repository<RecurringTransaction>(dbContext), IRecurringTransactionRepository
{
    public override async Task<IReadOnlyList<RecurringTransaction>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        return await DbSet.AsNoTracking()
            .Where(transaction => transaction.EndDate == null || transaction.EndDate >= today)
            .ToListAsync(cancellationToken);
    }

    public override async Task<RecurringTransaction?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var transaction = await base.GetByIdAsync(id, cancellationToken);
        return transaction?.EndDate is not { } endDate || endDate.Date >= DateTime.Today ? transaction : null;
    }
}
