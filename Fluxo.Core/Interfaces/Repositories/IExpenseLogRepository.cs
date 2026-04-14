using Fluxo.Core.Entities;

namespace Fluxo.Core.Interfaces.Repositories;

public interface IExpenseLogRepository : IRepository<ExpenseLog>
{
    Task<ExpenseLog?> GetByLogIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExpenseLog>> GetByExpenseIdAsync(int expenseId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExpenseLog>> GetMarkedForDeletionAsync(CancellationToken cancellationToken = default);
}