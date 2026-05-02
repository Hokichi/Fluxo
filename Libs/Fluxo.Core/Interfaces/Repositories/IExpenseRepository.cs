using Fluxo.Core.Entities;
using Fluxo.Core.Filters;

namespace Fluxo.Core.Interfaces.Repositories;

public interface IExpenseRepository : IRepository<Expense>
{
    Task<Expense?> GetByExpenseIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Expense>> SearchAsync(ExpenseFilter filter, CancellationToken cancellationToken = default);
}