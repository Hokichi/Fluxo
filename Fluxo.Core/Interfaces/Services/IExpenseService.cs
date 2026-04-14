using Fluxo.Core.DTO;
using Fluxo.Core.Filters;

namespace Fluxo.Core.Interfaces.Services;

public interface IExpenseService
{
    Task<IReadOnlyList<ExpenseDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExpenseDto>> SearchAsync(ExpenseFilter filter, CancellationToken cancellationToken = default);
    Task AddAsync(ExpenseDto dto, CancellationToken cancellationToken = default);
    Task UpdateAsync(ExpenseDto dto, CancellationToken cancellationToken = default);
    Task RemoveAsync(int id, CancellationToken cancellationToken = default);
}
