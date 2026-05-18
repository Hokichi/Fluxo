using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.Services.Logging;

namespace Fluxo.Services.Updates;

public sealed class StartupUpdateNotificationService(
    IAppUpdateService appUpdateService,
    IDataOperationRunner dataOperationRunner)
    : IStartupUpdateNotificationService
{
    private const string AppUpdatePrefix = "AppUpdate-";
    private const string UpdateHeader = "New Update Found";

    public async Task CheckAndSyncAsync(CancellationToken cancellationToken = default)
    {
        AppUpdateCheckResult updateCheckResult;

        try
        {
            updateCheckResult = await appUpdateService.CheckForUpdatesAsync(
                AppVersionResolver.ResolveCurrentVersion(),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            FluxoLogManager.LogWarning(exception, "Unable to check for updates during startup notification sync.");
            return;
        }

        switch (updateCheckResult.Status)
        {
            case AppUpdateCheckStatus.UpdateAvailable:
                if (string.IsNullOrWhiteSpace(updateCheckResult.LatestVersion))
                    return;

                await UpsertUpdateNotificationAsync(updateCheckResult, cancellationToken);
                return;

            case AppUpdateCheckStatus.UpToDate:
                await ClearAppUpdateNotificationsAsync(cancellationToken);
                return;

            case AppUpdateCheckStatus.Error:
            default:
                return;
        }
    }

    internal static Notification BuildNotificationForUpdate(AppUpdateCheckResult update, DateTime createdOn)
    {
        ArgumentNullException.ThrowIfNull(update);

        if (string.IsNullOrWhiteSpace(update.LatestVersion))
            throw new ArgumentException("Latest version is required.", nameof(update));

        var versionToken = update.LatestVersion.Trim();

        return new Notification
        {
            Type = $"{AppUpdatePrefix}{versionToken}",
            Header = UpdateHeader,
            Message = $"Version {versionToken} is available for download",
            CreatedOn = createdOn,
            IsCleared = false,
            IsForDeletion = false
        };
    }

    internal static bool IsAppUpdateNotification(Notification notification)
    {
        return notification.Type.StartsWith(AppUpdatePrefix, StringComparison.OrdinalIgnoreCase);
    }

    private async Task UpsertUpdateNotificationAsync(
        AppUpdateCheckResult update,
        CancellationToken cancellationToken)
    {
        var createdOn = DateTime.Now;
        var targetNotification = BuildNotificationForUpdate(update, createdOn);

        await dataOperationRunner.RunAsync(
            "sync startup update notifications",
            async (scope, ct) =>
            {
                var unitOfWork = scope.UnitOfWork;
                var persistedNotifications = await unitOfWork.Notifications.GetAllAsync(ct);
                var appUpdateNotifications = persistedNotifications
                    .Where(IsAppUpdateNotification)
                    .ToList();

                var retainedNotification = appUpdateNotifications
                    .OrderByDescending(notification => notification.CreatedOn)
                    .FirstOrDefault(notification =>
                        string.Equals(notification.Type, targetNotification.Type, StringComparison.OrdinalIgnoreCase));

                var hasChanges = false;

                if (retainedNotification is null)
                {
                    await unitOfWork.Notifications.AddAsync(targetNotification, ct);
                    hasChanges = true;
                }
                else
                {
                    retainedNotification.Type = targetNotification.Type;
                    retainedNotification.Header = targetNotification.Header;
                    retainedNotification.Message = targetNotification.Message;
                    retainedNotification.CreatedOn = targetNotification.CreatedOn;
                    retainedNotification.IsCleared = false;
                    retainedNotification.IsForDeletion = false;
                    unitOfWork.Notifications.Update(retainedNotification);
                    hasChanges = true;
                }

                foreach (var staleNotification in appUpdateNotifications.Where(notification =>
                             retainedNotification is null ||
                             notification.Id != retainedNotification.Id))
                {
                    if (staleNotification.IsCleared && staleNotification.IsForDeletion)
                        continue;

                    staleNotification.IsCleared = true;
                    staleNotification.IsForDeletion = true;
                    unitOfWork.Notifications.Update(staleNotification);
                    hasChanges = true;
                }

                if (hasChanges)
                    await unitOfWork.SaveChangesAsync(ct);
            },
            cancellationToken);
    }

    private async Task ClearAppUpdateNotificationsAsync(CancellationToken cancellationToken)
    {
        await dataOperationRunner.RunAsync(
            "clear startup update notifications",
            async (scope, ct) =>
            {
                var unitOfWork = scope.UnitOfWork;
                var persistedNotifications = await unitOfWork.Notifications.GetAllAsync(ct);
                var appUpdateNotifications = persistedNotifications
                    .Where(IsAppUpdateNotification)
                    .ToList();

                var hasChanges = false;

                foreach (var notification in appUpdateNotifications)
                {
                    if (notification.IsCleared && notification.IsForDeletion)
                        continue;

                    notification.IsCleared = true;
                    notification.IsForDeletion = true;
                    unitOfWork.Notifications.Update(notification);
                    hasChanges = true;
                }

                if (hasChanges)
                    await unitOfWork.SaveChangesAsync(ct);
            },
            cancellationToken);
    }
}
