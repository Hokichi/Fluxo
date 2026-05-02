using Fluxo.Core.DTO;
using Fluxo.Core.Filters;

namespace Fluxo.Core.Interfaces.Services;

public interface ITagService
{
    Task<IReadOnlyList<ExpenseTagDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExpenseTagDto>> GetTagsOrderedByExpenseCountAsync(ExpenseFilter filter, CancellationToken cancellationToken = default);
    Task AddAsync(ExpenseTagDto dto, CancellationToken cancellationToken = default);
    Task UpdateAsync(ExpenseTagDto dto, CancellationToken cancellationToken = default);
    Task RemoveAsync(int id, CancellationToken cancellationToken = default);
}
