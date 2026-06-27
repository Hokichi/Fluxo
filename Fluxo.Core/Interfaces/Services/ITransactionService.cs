using Fluxo.Core.DTO;

namespace Fluxo.Core.Interfaces.Services;

public interface ITransactionService
{
    Task<IReadOnlyList<TransactionDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<TransactionDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task PostTerminationCleanupAsync(CancellationToken cancellationToken = default);
}
