namespace Fluxo.Services.Notifications;

public interface IStartupNotificationSummaryService
{
    Task<StartupNotificationSummary?> BuildAsync(CancellationToken cancellationToken = default);
}
