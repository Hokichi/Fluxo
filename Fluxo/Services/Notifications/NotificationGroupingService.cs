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
                    Header = BuildHeader(category, count, orderedNotifications),
                    Message = BuildMessage(category, orderedNotifications),
                    Severity = ResolveSeverity(orderedNotifications.Select(notification => notification.Severity)),
                    HasActionCta = IsActionable(category),
                    LatestCreatedOn = orderedNotifications[0].CreatedOn
                };
            })
            .OrderByDescending(card => card.Category == NotificationGroupCategory.AppUpdate)
            .ThenByDescending(card => card.LatestCreatedOn)
            .ToList();
    }

    private static NotificationGroupCategory MapCategory(string type)
    {
        var typeToken = type.Split('_')[0];

        if (typeToken.StartsWith("RecurringTransactionDue", StringComparison.OrdinalIgnoreCase))
            return NotificationGroupCategory.RecurringTransactionDue;

        if (typeToken.StartsWith("RecurringTransactionOverdue", StringComparison.OrdinalIgnoreCase))
            return NotificationGroupCategory.RecurringTransactionOverdue;

        if (typeToken.StartsWith("GoalOverdue", StringComparison.OrdinalIgnoreCase))
            return NotificationGroupCategory.GoalOverdue;

        if (typeToken.StartsWith("DailyAllowance", StringComparison.OrdinalIgnoreCase))
            return NotificationGroupCategory.DailyAllowance;

        if (typeToken.StartsWith("UpcomingDeduction", StringComparison.OrdinalIgnoreCase))
            return NotificationGroupCategory.RecurringTransactionDue;

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

        if (typeToken.StartsWith("AppUpdate", StringComparison.OrdinalIgnoreCase))
            return NotificationGroupCategory.AppUpdate;

        return NotificationGroupCategory.Other;
    }

    private static bool IsActionable(NotificationGroupCategory category)
    {
        return category is NotificationGroupCategory.RecurringTransactionDue
            or NotificationGroupCategory.UpcomingPayment
            or NotificationGroupCategory.LatePayment
            or NotificationGroupCategory.GoalDeadline
            or NotificationGroupCategory.RecurringTransactionOverdue
            or NotificationGroupCategory.GoalOverdue
            or NotificationGroupCategory.AppUpdate;
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

    private static string BuildHeader(
        NotificationGroupCategory category,
        int count,
        IReadOnlyList<NotificationVM> notifications)
    {
        if (category == NotificationGroupCategory.AppUpdate)
            return notifications[0].Header;

        var label = category switch
        {
            NotificationGroupCategory.RecurringTransactionDue => "Recurring Transaction Due",
            NotificationGroupCategory.UpcomingPayment => "Upcoming Payment",
            NotificationGroupCategory.LatePayment => "Late Payment",
            NotificationGroupCategory.GoalDeadline => "Goal Deadline",
            NotificationGroupCategory.RecurringTransactionOverdue => "Recurring Transaction Overdue",
            NotificationGroupCategory.GoalOverdue => "Goal Overdue",
            NotificationGroupCategory.DailyAllowance => "Daily Allowance",
            NotificationGroupCategory.LowBalance => "Low Balance",
            NotificationGroupCategory.LowCredit => "Low Credit",
            NotificationGroupCategory.BudgetThreshold => "Budget Threshold",
            NotificationGroupCategory.AutoExpenseProcessed => "Auto Expense Processed",
            _ => "Notification"
        };

        return label;
    }

    private static string BuildMessage(NotificationGroupCategory category, IReadOnlyList<NotificationVM> notifications)
    {
        var count = notifications.Count;
        if (count == 0)
            return string.Empty;

        var newest = notifications[0];
        var suffix = count == 1 ? "1 pending item" : $"{count} pending items";

        return category switch
        {
            NotificationGroupCategory.AppUpdate => newest.Message,
            _ => suffix
        };
    }
}
