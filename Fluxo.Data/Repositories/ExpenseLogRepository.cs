using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;

namespace Fluxo.Data.Repositories;

public sealed class ExpenseLogRepository(FluxoDbContext dbContext)
    : Repository<ExpenseLog>(dbContext), IExpenseLogRepository
{
}