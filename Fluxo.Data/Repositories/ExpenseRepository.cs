using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;

namespace Fluxo.Data.Repositories;

public sealed class ExpenseRepository(FluxoDbContext dbContext)
    : Repository<Expense>(dbContext), IExpenseRepository
{
}