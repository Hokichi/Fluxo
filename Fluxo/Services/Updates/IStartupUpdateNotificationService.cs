namespace Fluxo.Services.Updates;

public interface IStartupUpdateNotificationService
{
    Task CheckAndSyncAsync(CancellationToken cancellationToken = default);
}
