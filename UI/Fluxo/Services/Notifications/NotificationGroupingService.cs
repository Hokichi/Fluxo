using System.Collections.ObjectModel;
using Fluxo.Core.Enums;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Shell.Main;

namespace Fluxo.Services.Notifications;

public sealed class NotificationGroupingService : INotificationGroupingService
{
    public IReadOnlyList<NotificationItemVM> Group(IReadOnlyList<NotificationVM> notifications)
    {
        if (notifications.Count == 0)
            return [];

        return notifications
            .GroupBy(notification => MapCategory(notification.Type))
            .Select(group =>
            {
                var orderedNotifications = group
                    .OrderByDescending(notification => notification.CreatedOn)
                    .ToList();
                var count = orderedNotifications.Count;
                var category = group.Key;

                return new NotificationItemVM
                {
                    Category = category,
                    Notifications = new ObservableCollection<NotificationVM>(orderedNotifications),
                    Count = count,
                    Header = BuildHeader(category, count),
                    Message = BuildMessage(category, orderedNotifications),
                    Severity = ResolveSeverity(orderedNotifications.Select(notification => notification.Severity)),
                    HasActionCta = IsActionable(category),
                    LatestCreatedOn = orderedNotifications[0].CreatedOn
                };
            })
            .OrderByDescending(card => card.LatestCreatedOn)
            .ToList();
    }

    private static NotificationGroupCategory MapCategory(string type)
    {
        var typeToken = type.Split('_')[0];

        if (typeToken.StartsWith("UpcomingDeduction", StringComparison.OrdinalIgnoreCase))
            return NotificationGroupCategory.FixedExpenseDue;

        if (typeToken.StartsWith("UpcomingPayment", StringComparison.OrdinalIgnoreCase))
            return NotificationGroupCategory.UpcomingPayment;

        if (typeToken.StartsWith("LatePayment", StringComparison.OrdinalIgnoreCase))
            return NotificationGroupCategory.LatePayment;

        if (typeToken.StartsWith("GoalDeadline", StringComparison.OrdinalIgnoreCase))
            return NotificationGroupCategory.GoalDeadline;

        if (typeToken.StartsWith("LowBalance", StringComparison.OrdinalIgnoreCase))
            return NotificationGroupCategory.LowBalance;

        if (typeToken.StartsWith("LowCredit", StringComparison.OrdinalIgnoreCase))
            return NotificationGroupCategory.LowCredit;

        if (typeToken.StartsWith("BudgetThreshold", StringComparison.OrdinalIgnoreCase))
            return NotificationGroupCategory.BudgetThreshold;

        if (typeToken.StartsWith("AutoExpenseProcessed", StringComparison.OrdinalIgnoreCase))
            return NotificationGroupCategory.AutoExpenseProcessed;

        return NotificationGroupCategory.Other;
    }

    private static bool IsActionable(NotificationGroupCategory category)
    {
        return category is NotificationGroupCategory.FixedExpenseDue
            or NotificationGroupCategory.UpcomingPayment
            or NotificationGroupCategory.LatePayment
            or NotificationGroupCategory.GoalDeadline;
    }

    private static NotificationSeverity ResolveSeverity(IEnumerable<NotificationSeverity> severities)
    {
        var maxRank = severities
            .Select(GetSeverityRank)
            .DefaultIfEmpty(GetSeverityRank(NotificationSeverity.Info))
            .Max();

        return maxRank switch
        {
            4 => NotificationSeverity.Danger,
            3 => NotificationSeverity.Warning,
            2 => NotificationSeverity.Success,
            _ => NotificationSeverity.Info
        };
    }

    private static int GetSeverityRank(NotificationSeverity severity)
    {
        return severity switch
        {
            NotificationSeverity.Danger => 4,
            NotificationSeverity.Warning => 3,
            NotificationSeverity.Success => 2,
            _ => 1
        };
    }

    private static string BuildHeader(NotificationGroupCategory category, int count)
    {
        var label = category switch
        {
            NotificationGroupCategory.FixedExpenseDue => "Fixed Expense Due",
            NotificationGroupCategory.UpcomingPayment => "Upcoming Payment",
            NotificationGroupCategory.LatePayment => "Late Payment",
            NotificationGroupCategory.GoalDeadline => "Goal Deadline",
            NotificationGroupCategory.LowBalance => "Low Balance",
            NotificationGroupCategory.LowCredit => "Low Credit",
            NotificationGroupCategory.BudgetThreshold => "Budget Threshold",
            NotificationGroupCategory.AutoExpenseProcessed => "Auto Expense Processed",
            _ => "Notification"
        };

        return count == 1 ? label : $"{label} ({count})";
    }

    private static string BuildMessage(NotificationGroupCategory category, IReadOnlyList<NotificationVM> notifications)
    {
        var count = notifications.Count;
        if (count == 0)
            return string.Empty;

        var newest = notifications[0];
        var suffix = count == 1 ? "1 item" : $"{count} items";

        return category switch
        {
            NotificationGroupCategory.FixedExpenseDue => $"{suffix}: {newest.Message}",
            NotificationGroupCategory.UpcomingPayment => $"{suffix}: {newest.Message}",
            NotificationGroupCategory.LatePayment => $"{suffix}: {newest.Message}",
            NotificationGroupCategory.GoalDeadline => $"{suffix}: {newest.Message}",
            NotificationGroupCategory.LowBalance => $"{suffix}: {newest.Message}",
            NotificationGroupCategory.LowCredit => $"{suffix}: {newest.Message}",
            NotificationGroupCategory.BudgetThreshold => $"{suffix}: {newest.Message}",
            NotificationGroupCategory.AutoExpenseProcessed => $"{suffix}: {newest.Message}",
            _ => $"{suffix}: {newest.Message}"
        };
    }
}
