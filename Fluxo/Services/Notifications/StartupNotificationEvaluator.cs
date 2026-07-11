using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Constants;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.Resources.Resources.Messages;
using Fluxo.ViewModels.Entities;

namespace Fluxo.Services.Notifications;

public sealed record StartupNotificationEvaluation(
    IReadOnlySet<int> OverdueAccountIds,
    IReadOnlySet<int> OverdueRecurringTransactionIds,
    IReadOnlySet<int> OverdueSavingGoalIds,
    IReadOnlyList<NotificationVM> Notifications);

public sealed class StartupNotificationEvaluator(
    IDataOperationRunner dataOperationRunner,
    Func<DateTime>? clock = null)
{
    public Task<StartupNotificationEvaluation> EvaluateAsync(CancellationToken cancellationToken = default) =>
        EvaluateAsync(null, null, cancellationToken);

    public Task<StartupNotificationEvaluation> EvaluateEntityAsync(
        NotificationEntityKind kind,
        int entityId,
        CancellationToken cancellationToken = default) =>
        EvaluateAsync(kind, entityId, cancellationToken);

    private Task<StartupNotificationEvaluation> EvaluateAsync(
        NotificationEntityKind? requestedKind,
        int? requestedId,
        CancellationToken cancellationToken) =>
        dataOperationRunner.RunAsync(async (scope, ct) =>
        {
            var unitOfWork = scope.UnitOfWork;
            var today = (clock?.Invoke() ?? DateTime.Today).Date;
            var transactions = (await unitOfWork.Transactions.GetAllAsync(ct))
                .Where(transaction => !transaction.IsForDeletion).ToList();
            var accounts = (await unitOfWork.Accounts.GetAllAsync(ct))
                .Where(account => !account.IsForDeletion && account.IsEnabled).ToList();
            var recurring = (await unitOfWork.RecurringTransactions.GetAllAsync(ct))
                .Where(item => item.IsEnabled && (item.EndDate is null || item.EndDate.Value.Date >= today)).ToList();
            var goals = await unitOfWork.SavingGoals.GetAllAsync(ct);
            var settings = (await unitOfWork.UserSettings.GetAllAsync(ct)).ToDictionary(setting => setting.Name, setting => setting.Value);
            var allocation = await unitOfWork.BudgetAllocation.GetAsync(ct);
            var recurringEnabled = Enabled(UserSettingNames.IsRecurringOverdueNotifEnabled, true);
            var goalsEnabled = Enabled(UserSettingNames.IsGoalOverdueNotifEnabled, true);
            var dailyAllowanceEnabled = Enabled(UserSettingNames.IsDailyAllowanceNotifEnabled, true);

            var overdueAccounts = accounts.Where(account => IsCreditOverdue(account, transactions, today))
                .Select(account => account.Id).ToHashSet();
            var overdueRecurring = recurring.Where(item => IsRecurringOverdue(item, transactions, today))
                .Select(item => item.Id).ToHashSet();
            var overdueGoals = goals.Where(goal => IsGoalOverdue(goal, today))
                .Select(goal => goal.Id).ToHashSet();

            var notifications = new List<NotificationVM>();
            if (requestedKind is not NotificationEntityKind.RecurringTransaction)
                notifications.AddRange(accounts.Where(account => overdueAccounts.Contains(account.Id)).Select(account => Card(
                    $"LatePayment-{account.Id}", $"Late Payment - {account.Name}",
                    $"{account.Name} payment is overdue.", NotificationSeverity.Danger)));
            if (recurringEnabled && requestedKind is not NotificationEntityKind.Account and not NotificationEntityKind.SavingGoal)
                notifications.AddRange(recurring.Where(item => overdueRecurring.Contains(item.Id)).Select(item => Card(
                    $"RecurringTransactionOverdue-{item.Id}", $"Recurring Transaction Overdue - {item.Name}",
                    $"{item.Name} needs completion.", NotificationSeverity.Warning)));
            if (goalsEnabled && requestedKind is not NotificationEntityKind.Account and not NotificationEntityKind.RecurringTransaction)
                notifications.AddRange(goals.Where(goal => overdueGoals.Contains(goal.Id)).Select(goal => Card(
                    $"GoalOverdue-{goal.Id}", $"Goal Overdue - {goal.Name}",
                    $"{goal.Name} is past its target date.", NotificationSeverity.Danger)));
            var dailyAllowance = allocation is { AllocationLimit: > 0m } ? allocation.AllocationLimit / DateTime.DaysInMonth(today.Year, today.Month) : 0m;
            var todaySpending = transactions.Where(transaction => transaction.Type == TransactionType.Expense && transaction.OccurredOn.Date == today && !transaction.IsExcludedFromBudget).Sum(transaction => transaction.Amount);
            if (dailyAllowanceEnabled && dailyAllowance > 0m && todaySpending >= dailyAllowance)
                notifications.Add(Card("DailyAllowance", "Daily Allowance Reached", "Today's spending reached the daily allowance.", NotificationSeverity.Warning));

            if (requestedKind is not null && requestedId is not null)
                notifications = notifications.Where(card => card.Type.EndsWith($"-{requestedId}", StringComparison.Ordinal)).ToList();

            return new StartupNotificationEvaluation(overdueAccounts, overdueRecurring, overdueGoals, notifications);

            bool Enabled(string name, bool fallback) => !settings.TryGetValue(name, out var value) || !bool.TryParse(value, out var enabled) ? fallback : enabled;
        }, cancellationToken);

    private static bool IsCreditOverdue(Account account, IReadOnlyList<Transaction> transactions, DateTime today)
    {
        if (account.AccountType != AccountType.Credit || account.MonthlyDueDate is not { } dueDay || account.SpentAmount <= 0)
            return false;
        var dueDate = new DateTime(today.Year, today.Month, Math.Min(dueDay, DateTime.DaysInMonth(today.Year, today.Month)));
        if (today <= dueDate)
            return false;
        return !transactions.Any(transaction => transaction.RepaymentAccountId == account.Id && transaction.OccurredOn.Date >= dueDate);
    }

    private static bool IsRecurringOverdue(RecurringTransaction recurring, IReadOnlyList<Transaction> transactions, DateTime today)
    {
        var dueDate = ResolveCurrentOccurrence(recurring, today);
        if (dueDate is null || dueDate > today)
            return false;
        var nextDueDate = ResolveNextOccurrence(recurring, dueDate.Value);
        return !transactions.Any(transaction => transaction.RelatedRecurringTransactionId == recurring.Id &&
            transaction.OccurredOn.Date >= dueDate && transaction.OccurredOn.Date < nextDueDate);
    }

    private static bool IsGoalOverdue(SavingGoal goal, DateTime today) =>
        goal.SavingEndDate is { } endDate && endDate.Date < today && goal.TargetAmount > goal.CurrentAmount;

    private static DateTime? ResolveCurrentOccurrence(RecurringTransaction item, DateTime today) => item.RecurringPeriod switch
    {
        RecurringPeriod.Weekly or RecurringPeriod.Biweekly when item.RecurringTime is >= 1 and <= 7 =>
            today.AddDays(-(((int)today.DayOfWeek + 6) % 7 - (item.RecurringTime - 1) + 7) % 7),
        RecurringPeriod.Monthly when item.RecurringTime > 0 => new DateTime(today.Year, today.Month,
            Math.Min(item.RecurringTime, DateTime.DaysInMonth(today.Year, today.Month))),
        _ => null
    };

    private static DateTime ResolveNextOccurrence(RecurringTransaction item, DateTime occurrence) => item.RecurringPeriod switch
    {
        RecurringPeriod.Biweekly => occurrence.AddDays(14),
        RecurringPeriod.Weekly => occurrence.AddDays(7),
        RecurringPeriod.Monthly => occurrence.AddMonths(1),
        _ => occurrence.AddDays(1)
    };

    private static NotificationVM Card(string type, string header, string message, NotificationSeverity severity) => new()
    {
        Type = type, Header = header, Message = message, Severity = severity, CreatedOn = DateTime.Now
    };
}
