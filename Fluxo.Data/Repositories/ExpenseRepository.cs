using Fluxo.Core.Entities;
using Fluxo.Core.Filters;
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

    public async Task<Expense?> GetByExpenseIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (FindTrackedEntity(id) is { } tracked)
            return tracked;

        return await QueryWithNavigations()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Expense>> SearchAsync(ExpenseFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = QueryWithNavigations();

        if (!string.IsNullOrWhiteSpace(filter.Name))
            query = query.Where(e => e.Name.Contains(filter.Name));

        if (filter.Category.HasValue)
            query = query.Where(e => e.ExpenseCategory == filter.Category);

        if (filter.TagId.HasValue)
            query = query.Where(e => e.TagId == filter.TagId);
        else if (filter.Tag is not null)
            query = query.Where(e => e.TagId == filter.Tag.Id);

        return await query.ToListAsync(cancellationToken);
    }

    private IQueryable<Expense> QueryWithNavigations()
    {
        return DbSet
            .AsNoTrackingWithIdentityResolution()
            .Include(e => e.Tag)
            .Include(e => e.Account);
    }
}
