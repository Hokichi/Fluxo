using Fluxo.Core.DTO;

namespace Fluxo.Core.Interfaces.Services;

public interface IExpenseLogService
{
    Task<IReadOnlyList<ExpenseLogDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ExpenseLogDto?> GetByLogIdAsync(int id, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task PostTerminationCleanupAsync(CancellationToken cancellationToken = default);
}
