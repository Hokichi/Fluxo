using System.Globalization;
using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Popups;
using Fluxo.ViewModels.Shell.Main;

namespace Fluxo.Services.Notifications;

public sealed class NotificationActionService(IDataOperationRunner dataOperationRunner) : INotificationActionService
{
    public Task<bool> ExecuteChecklistActionAsync(
        NotificationItemVM card,
        IReadOnlyCollection<NotificationChecklistActionDecision> decisions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(decisions);

        var selectedIds = decisions
            .Where(decision => decision.Action != NotificationChecklistItemActionType.Ignore)
            .Select(decision => decision.EntityId)
            .Distinct()
            .ToArray();

        return ExecuteChecklistActionBySelectedIdsAsync(card, selectedIds, cancellationToken);
    }

    private Task<bool> ExecuteChecklistActionBySelectedIdsAsync(
        NotificationItemVM card,
        IReadOnlyCollection<int> selectedIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(selectedIds);

        if (card.Notifications.Count == 0 || selectedIds.Count == 0)
            return Task.FromResult(false);

        var prefix = GetChecklistPrefix(card.Category);
        if (prefix.Length == 0)
            return Task.FromResult(false);

        var selectedIdSet = selectedIds.ToHashSet();
        var selectedTypeSet = card.Notifications
            .Where(notification => TryExtractNotificationEntityId(notification, prefix, out var entityId) &&
                                   selectedIdSet.Contains(entityId))
            .Select(notification => notification.Type)
            .ToHashSet(StringComparer.Ordinal);

        if (selectedTypeSet.Count == 0)
            return Task.FromResult(false);

        return dataOperationRunner.RunAsync(async (scope, ct) =>
        {
            var unitOfWork = scope.UnitOfWork;
            var persistedNotifications = await unitOfWork.Notifications.GetAllAsync(ct);
            var matched = persistedNotifications
                .Where(notification => !notification.IsCleared && selectedTypeSet.Contains(notification.Type))
                .ToList();

            if (matched.Count == 0)
                return false;

            foreach (var notification in matched)
            {
                notification.IsCleared = true;
                unitOfWork.Notifications.Update(notification);
            }

            await unitOfWork.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);
    }

    public Task<bool> ExecuteGoalActionAsync(
        NotificationItemVM card,
        GoalDeadlineActionType actionType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (card.Notifications.Count == 0 || actionType == GoalDeadlineActionType.None)
            return Task.FromResult(false);

        var goalIds = card.Notifications
            .Where(notification => TryExtractNotificationEntityId(notification, "GoalDeadline-", out _))
            .Select(notification => ExtractNotificationEntityId(notification, "GoalDeadline-"))
            .Distinct()
            .ToArray();

        if (goalIds.Length == 0)
            return Task.FromResult(false);

        return dataOperationRunner.RunAsync(async (scope, ct) =>
        {
            var unitOfWork = scope.UnitOfWork;
            var mutationApplied = false;

            foreach (var goalId in goalIds)
            {
                var goal = await unitOfWork.SavingGoals.GetByIdAsync(goalId, ct);
                if (goal is null)
                    continue;

                switch (actionType)
                {
                    case GoalDeadlineActionType.MarkAsReached:
                        goal.CurrentAmount = goal.TargetAmount;
                        unitOfWork.SavingGoals.Update(goal);
                        mutationApplied = true;
                        break;
                    case GoalDeadlineActionType.AbandonGoal:
                        unitOfWork.SavingGoals.Remove(goal);
                        mutationApplied = true;
                        break;
                }
            }

            if (!mutationApplied)
                return false;

            var goalIdSet = goalIds.ToHashSet();
            var persistedNotifications = await unitOfWork.Notifications.GetAllAsync(ct);
            var matchedNotifications = persistedNotifications
                .Where(notification =>
                    !notification.IsCleared &&
                    TryExtractNotificationEntityId(notification.Type, "GoalDeadline-", out var entityId) &&
                    goalIdSet.Contains(entityId))
                .ToList();

            foreach (var notification in matchedNotifications)
            {
                notification.IsCleared = true;
                unitOfWork.Notifications.Update(notification);
            }

            await unitOfWork.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);
    }

    private static string GetChecklistPrefix(NotificationGroupCategory category)
    {
        return category switch
        {
            NotificationGroupCategory.FixedExpenseDue => "UpcomingDeduction-",
            NotificationGroupCategory.UpcomingPayment => "UpcomingPayment-",
            NotificationGroupCategory.LatePayment => "LatePayment-",
            _ => string.Empty
        };
    }

    private static bool TryExtractNotificationEntityId(NotificationVM notification, string prefix, out int entityId)
    {
        return TryExtractNotificationEntityId(notification.Type, prefix, out entityId);
    }

    private static int ExtractNotificationEntityId(NotificationVM notification, string prefix)
    {
        return ExtractNotificationEntityId(notification.Type, prefix);
    }

    private static int ExtractNotificationEntityId(string notificationType, string prefix)
    {
        return TryExtractNotificationEntityId(notificationType, prefix, out var entityId) ? entityId : 0;
    }

    private static bool TryExtractNotificationEntityId(string notificationType, string prefix, out int entityId)
    {
        entityId = 0;
        if (string.IsNullOrWhiteSpace(notificationType) || string.IsNullOrWhiteSpace(prefix))
            return false;

        var typeToken = notificationType.Split('_')[0];
        if (!typeToken.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var idToken = typeToken[prefix.Length..];
        return int.TryParse(idToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out entityId);
    }
}
