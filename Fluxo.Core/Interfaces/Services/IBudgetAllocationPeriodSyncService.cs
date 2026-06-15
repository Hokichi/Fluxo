using Fluxo.Core.Interfaces;

namespace Fluxo.Core.Interfaces.Services;

public interface IBudgetAllocationPeriodSyncService
{
    Task SyncAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default);
}
