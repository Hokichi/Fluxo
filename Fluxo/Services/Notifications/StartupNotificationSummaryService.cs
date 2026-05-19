using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.Services.Logging;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Shell.Main;

namespace Fluxo.Services.Notifications;

public sealed class StartupNotificationSummaryService(
    IDataOperationRunner dataOperationRunner,
    INotificationGroupingService notificationGroupingService)
    : IStartupNotificationSummaryService
{
    public async Task<StartupNotificationSummary?> BuildAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await dataOperationRunner.RunAsync("build startup notification summary", async (scope, ct) =>
            {
                var activeNotifications = await scope.UnitOfWork.Notifications.GetActiveAsync(ct);
                if (activeNotifications.Count == 0)
                    return null;

                var visibleNotifications = activeNotifications
                    .Where(notification => !notification.IsCleared && !notification.IsForDeletion)
                    .ToList();
                if (visibleNotifications.Count == 0)
                    return null;

                var notificationViewModels = visibleNotifications
                    .Select(MapToViewModel)
                    .OrderByDescending(notification => notification.CreatedOn)
                    .ToList();

                var groupedNotifications = notificationGroupingService.Group(notificationViewModels);
                if (groupedNotifications.Count == 0)
                    return null;

                var primaryGroup = groupedNotifications[0];
                var primaryHeader = ResolvePrimaryHeader(primaryGroup);
                var primaryEntityName = ExtractName(primaryHeader);
                var message = groupedNotifications.Count > 1
                    ? $"There are {groupedNotifications.Count} notifications"
                    : BuildSingleGroupMessage(primaryGroup.Category, primaryGroup.Count, primaryHeader, primaryEntityName);

                return new StartupNotificationSummary(
                    GroupCount: groupedNotifications.Count,
                    PrimaryGroupCategory: primaryGroup.Category,
                    PrimaryGroupItemCount: primaryGroup.Count,
                    PrimaryHeader: primaryHeader,
                    PrimaryEntityName: primaryEntityName,
                    Message: message);
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            FluxoLogManager.LogWarning(exception, "Failed to build startup notification summary.");
            return null;
        }
    }

    private static NotificationVM MapToViewModel(Notification notification)
    {
        return new NotificationVM
        {
            Type = notification.Type,
            Header = notification.Header,
            Message = notification.Message,
            CreatedOn = notification.CreatedOn,
            IsCleared = notification.IsCleared
        };
    }

    private static string BuildSingleGroupMessage(
        NotificationGroupCategory category,
        int count,
        string primaryHeader,
        string primaryName)
    {
        return category switch
        {
            NotificationGroupCategory.RecurringTransactionDue => count == 1
                ? $"{primaryName} is due"
                : $"There are {count} recurring transactions due",
            NotificationGroupCategory.UpcomingPayment => count == 1
                ? $"{primaryName} is due"
                : $"There are {count} credit cards due",
            NotificationGroupCategory.GoalDeadline => count == 1
                ? $"Goal {primaryName} is reaching its deadline"
                : $"There are {count} goals reaching their deadlines",
            NotificationGroupCategory.LatePayment => count == 1
                ? "There is one late payment due"
                : $"There are {count} late payments due",
            _ => count == 1
                ? primaryHeader
                : $"There are {count} notifications"
        };
    }

    private static string ResolvePrimaryHeader(NotificationItemVM group)
    {
        var header = group.Notifications
            .OrderByDescending(notification => notification.CreatedOn)
            .Select(notification => notification.Header)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(header))
            return header;

        return group.Header;
    }

    private static string ExtractName(string header)
    {
        if (string.IsNullOrWhiteSpace(header))
            return header;

        var separator = " - ";
        var separatorIndex = header.IndexOf(separator, StringComparison.Ordinal);
        if (separatorIndex < 0)
            return header.Trim();

        var trailingText = header[(separatorIndex + separator.Length)..].Trim();
        return trailingText.Length == 0 ? header.Trim() : trailingText;
    }
}
