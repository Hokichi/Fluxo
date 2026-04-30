namespace Fluxo.Services.Notifications;

public interface IStartupNotificationSummaryService
{
    Task<StartupNotificationSummary?> GetSummaryAsync(CancellationToken cancellationToken = default);
}
