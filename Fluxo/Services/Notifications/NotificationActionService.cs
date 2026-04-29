using System.Globalization;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
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

        if (card.Notifications.Count == 0 || decisions.Count == 0)
            return Task.FromResult(false);

        var prefix = GetChecklistPrefix(card.Category);
        if (prefix.Length == 0)
            return Task.FromResult(false);

        var notificationTypesByEntityId = card.Notifications
            .Where(notification => TryExtractNotificationEntityId(notification, prefix, out _))
            .GroupBy(notification => ExtractNotificationEntityId(notification, prefix))
            .ToDictionary(
                group => group.Key,
                group => group.Select(notification => notification.Type)
                    .ToHashSet(StringComparer.Ordinal));

        var actionableDecisions = decisions
            .Where(decision =>
                decision.Action != NotificationChecklistItemActionType.Ignore &&
                notificationTypesByEntityId.ContainsKey(decision.EntityId))
            .ToList();

        if (actionableDecisions.Count == 0)
            return Task.FromResult(false);

        return dataOperationRunner.RunAsync(async (scope, ct) =>
        {
            var unitOfWork = scope.UnitOfWork;
            var persistedNotifications = await unitOfWork.Notifications.GetAllAsync(ct);
            var mutationApplied = false;

            foreach (var decision in actionableDecisions)
            {
                if (!notificationTypesByEntityId.TryGetValue(decision.EntityId, out var decisionNotificationTypes))
                    continue;

                switch (decision.Action)
                {
                    case NotificationChecklistItemActionType.Paid:
                        if (ClearMatchedNotifications(unitOfWork, persistedNotifications, decisionNotificationTypes))
                            mutationApplied = true;
                        break;
                    case NotificationChecklistItemActionType.Process:
                        var rowProcessed = card.Category switch
                        {
                            NotificationGroupCategory.UpcomingPayment or NotificationGroupCategory.LatePayment =>
                                await ProcessPaymentAsync(unitOfWork, decision.EntityId, ct),
                            NotificationGroupCategory.FixedExpenseDue =>
                                await ProcessFixedExpenseAsync(unitOfWork, decision, ct),
                            _ => false
                        };

                        if (!rowProcessed)
                            continue;

                        mutationApplied = true;
                        if (ClearMatchedNotifications(unitOfWork, persistedNotifications, decisionNotificationTypes))
                            mutationApplied = true;
                        break;
                }
            }

            if (!mutationApplied)
                return false;

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

    private static bool ClearMatchedNotifications(
        IUnitOfWork unitOfWork,
        IReadOnlyCollection<Notification> persistedNotifications,
        IReadOnlySet<string> matchedTypes)
    {
        var clearedAny = false;
        foreach (var notification in persistedNotifications.Where(notification =>
                     !notification.IsCleared && matchedTypes.Contains(notification.Type)))
        {
            notification.IsCleared = true;
            unitOfWork.Notifications.Update(notification);
            clearedAny = true;
        }

        return clearedAny;
    }

    private static async Task<bool> ProcessPaymentAsync(
        IUnitOfWork unitOfWork,
        int spendingSourceId,
        CancellationToken cancellationToken)
    {
        var targetSource = await unitOfWork.SpendingSources.GetByIdAsync(spendingSourceId, cancellationToken);
        if (targetSource is null ||
            targetSource.SpendingSourceType is not (SpendingSourceType.Credit or SpendingSourceType.BNPL) ||
            targetSource.DeductSource is not > 0 ||
            targetSource.SpentAmount <= 0m)
        {
            return false;
        }

        var deductingSource = await unitOfWork.SpendingSources.GetByIdAsync(targetSource.DeductSource.Value, cancellationToken);
        if (deductingSource is null)
            return false;

        var paymentTag = await ResolvePaymentTagAsync(unitOfWork, cancellationToken);
        if (paymentTag is null)
            return false;

        var amount = targetSource.SpentAmount;
        var processedOn = DateTime.Now;
        var expense = new Expense
        {
            Name = $"Payment to {targetSource.Name}",
            Amount = amount,
            ExpenseKind = ExpenseKind.Manual,
            ExpenseCategory = ExpenseCategory.Savings,
            RecurringDate = processedOn.Day,
            IsActive = false,
            SpendingSourceId = deductingSource.Id,
            ExpenseTagId = paymentTag.Id
        };

        await unitOfWork.Expenses.AddAsync(expense, cancellationToken);

        var expenseLog = new ExpenseLog
        {
            Expense = expense,
            SpendingSourceId = deductingSource.Id,
            Amount = amount,
            DeductedOn = processedOn,
            Notes = $"Payment to {targetSource.Name}",
            IsForDeletion = false
        };
        await unitOfWork.ExpenseLogs.AddAsync(expenseLog, cancellationToken);

        var incomeLog = new IncomeLog
        {
            SpendingSourceId = targetSource.Id,
            Amount = amount,
            AddedOn = processedOn,
            Notes = $"Payment from {deductingSource.Name}"
        };
        await unitOfWork.IncomeLogs.AddAsync(incomeLog, cancellationToken);

        ApplyExpenseToSpendingSource(deductingSource, amount);
        ApplyIncomeToSpendingSource(targetSource, amount);
        unitOfWork.SpendingSources.Update(deductingSource);
        unitOfWork.SpendingSources.Update(targetSource);

        return true;
    }

    private static async Task<bool> ProcessFixedExpenseAsync(
        IUnitOfWork unitOfWork,
        NotificationChecklistActionDecision decision,
        CancellationToken cancellationToken)
    {
        if (decision.SelectedSourceId is not > 0)
            return false;

        var expense = await unitOfWork.Expenses.GetByExpenseIdAsync(decision.EntityId, cancellationToken);
        if (expense is null || expense.Amount <= 0m)
            return false;

        var payingSource = await unitOfWork.SpendingSources.GetByIdAsync(decision.SelectedSourceId.Value, cancellationToken);
        if (payingSource is null)
            return false;

        var expenseLog = new ExpenseLog
        {
            Expense = expense,
            SpendingSourceId = payingSource.Id,
            Amount = expense.Amount,
            DeductedOn = DateTime.Now,
            Notes = expense.Name,
            IsForDeletion = false
        };

        await unitOfWork.ExpenseLogs.AddAsync(expenseLog, cancellationToken);

        ApplyExpenseToSpendingSource(payingSource, expense.Amount);
        unitOfWork.SpendingSources.Update(payingSource);

        return true;
    }

    private static async Task<ExpenseTag?> ResolvePaymentTagAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        var tags = await unitOfWork.ExpenseTags.GetAllAsync(cancellationToken);
        return tags
            .OrderByDescending(tag => string.Equals(tag.Name, "Transfer", StringComparison.OrdinalIgnoreCase))
            .ThenBy(tag => tag.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static void ApplyExpenseToSpendingSource(SpendingSource spendingSource, decimal amount)
    {
        if (spendingSource.SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL)
        {
            spendingSource.SpentAmount += amount;
            return;
        }

        spendingSource.Balance -= amount;
    }

    private static void ApplyIncomeToSpendingSource(SpendingSource spendingSource, decimal amount)
    {
        if (spendingSource.SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL)
        {
            spendingSource.SpentAmount = Math.Max(0m, spendingSource.SpentAmount - amount);
            return;
        }

        spendingSource.Balance += amount;
    }
}
