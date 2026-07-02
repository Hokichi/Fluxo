using System.Globalization;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Popups;
using Fluxo.ViewModels.Shell.Main;
using Fluxo.Services.Transactions;

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

        var notificationTypesByEntityId = card.Category == NotificationGroupCategory.RecurringTransactionDue
            ? card.Notifications
                .Where(notification => TryExtractRecurringEntityId(notification.Type, out _))
                .GroupBy(notification => ExtractRecurringEntityId(notification.Type))
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(notification => notification.Type).ToHashSet(StringComparer.Ordinal))
            : card.Notifications
                .Where(notification => TryExtractNotificationEntityId(notification, prefix, out _))
                .GroupBy(notification => ExtractNotificationEntityId(notification, prefix))
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(notification => notification.Type).ToHashSet(StringComparer.Ordinal));

        var actionableDecisions = decisions
            .Where(decision =>
                decision.Action != NotificationChecklistItemActionType.Ignore &&
                notificationTypesByEntityId.ContainsKey(decision.EntityId))
            .ToList();

        if (actionableDecisions.Count == 0)
            return Task.FromResult(false);

        return dataOperationRunner.RunAsync("process checklist notification action", async (scope, ct) =>
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
                                await ProcessPaymentAsync(
                                    unitOfWork,
                                    decision,
                                    card.Category == NotificationGroupCategory.LatePayment,
                                    ct),
                            NotificationGroupCategory.RecurringTransactionDue =>
                                await ProcessRecurringTransactionAsync(unitOfWork, decision, ct),
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

        return dataOperationRunner.RunAsync("process goal deadline notification action", async (scope, ct) =>
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
            NotificationGroupCategory.RecurringTransactionDue => "RecurringTransactionDue-",
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
        NotificationChecklistActionDecision decision,
        bool requireSelectedValues,
        CancellationToken cancellationToken)
    {
        var targetSource = await unitOfWork.Accounts.GetByIdAsync(decision.EntityId, cancellationToken);
        if (targetSource is null ||
            targetSource.AccountType != AccountType.Credit ||
            targetSource.SpentAmount <= 0m)
        {
            return false;
        }

        var sourceId = decision.SelectedSourceId ?? (requireSelectedValues ? null : targetSource.DeductSource);
        var amount = decision.Amount ?? (requireSelectedValues ? null : targetSource.SpentAmount);
        if (sourceId is not > 0 || amount is not > 0m || amount > targetSource.SpentAmount)
            return false;

        var deductingSource = await unitOfWork.Accounts.GetByIdAsync(sourceId.Value, cancellationToken);
        if (deductingSource is null || deductingSource.AccountType != AccountType.Checking)
            return false;

        var paymentTag = requireSelectedValues
            ? await ResolveBalanceUpdateTagAsync(unitOfWork, cancellationToken)
            : await ResolvePaymentTagAsync(unitOfWork, cancellationToken);
        if (paymentTag is null)
            return false;

        var pair = RepaymentTransactionSupport.Create(
            deductingSource,
            targetSource,
            amount.Value,
            DateTime.Now,
            paymentTag);
        await unitOfWork.Transactions.AddAsync(pair.Expense, cancellationToken);
        await unitOfWork.Transactions.AddAsync(pair.Income, cancellationToken);
        unitOfWork.Accounts.Update(deductingSource);
        unitOfWork.Accounts.Update(targetSource);

        return true;
    }

    private static async Task<bool> ProcessRecurringTransactionAsync(
        IUnitOfWork unitOfWork,
        NotificationChecklistActionDecision decision,
        CancellationToken cancellationToken)
    {
        var recurring = await unitOfWork.RecurringTransactions.GetByIdAsync(decision.EntityId, cancellationToken);
        if (recurring is null || decision.Amount is not > 0m || decision.SelectedSourceId is not > 0)
            return false;

        if (decision.UpdateRecurringAmount)
        {
            recurring.Amount = decision.Amount.Value;
            unitOfWork.RecurringTransactions.Update(recurring);
        }

        var source = await unitOfWork.Accounts.GetByIdAsync(decision.SelectedSourceId.Value, cancellationToken);
        if (source is null)
            return false;

        var amount = decision.Amount.Value;
        return recurring.Type switch
        {
            RecurringTransactionType.Expense or RecurringTransactionType.Income =>
                await AddRecurringTransactionAsync(unitOfWork, recurring, source, amount, decision.SelectedTagId, cancellationToken),
            RecurringTransactionType.GoalUpdate => await AddRecurringGoalUpdateAsync(unitOfWork, recurring, source, amount, decision.SelectedGoalId, cancellationToken),
            _ => false
        };
    }

    private static bool TryExtractRecurringEntityId(string notificationType, out int entityId)
    {
        return TryExtractNotificationEntityId(notificationType, "RecurringTransactionDue-", out entityId) ||
               TryExtractNotificationEntityId(notificationType, "UpcomingDeduction-", out entityId);
    }

    private static int ExtractRecurringEntityId(string notificationType)
    {
        if (TryExtractNotificationEntityId(notificationType, "RecurringTransactionDue-", out var entityId))
            return entityId;
        return TryExtractNotificationEntityId(notificationType, "UpcomingDeduction-", out entityId) ? entityId : 0;
    }

    private static async Task<bool> AddRecurringTransactionAsync(
        IUnitOfWork unitOfWork,
        RecurringTransaction recurring,
        Account source,
        decimal amount,
        int? selectedTagId,
        CancellationToken cancellationToken)
    {
        if (selectedTagId is not > 0)
            return false;

        var tag = await unitOfWork.Tags.GetByIdAsync(selectedTagId.Value, cancellationToken);
        if (tag is null)
            return false;

        var transactionType = recurring.Type == RecurringTransactionType.Expense
            ? TransactionType.Expense
            : TransactionType.Income;
        var transaction = new Transaction
        {
            Type = transactionType,
            Name = recurring.Name,
            Amount = amount,
            OccurredOn = DateTime.Now,
            Notes = recurring.Name,
            ExpenseCategory = transactionType == TransactionType.Expense
                ? recurring.Category ?? ExpenseCategory.Needs
                : null,
            SourceAccountId = source.Id,
            TagId = tag.Id
        };
        await unitOfWork.Transactions.AddAsync(transaction, cancellationToken);
        if (transactionType == TransactionType.Expense)
            ApplyExpenseToAccount(source, amount);
        else
            ApplyIncomeToAccount(source, amount);
        unitOfWork.Accounts.Update(source);
        return true;
    }

    private static async Task<bool> AddRecurringGoalUpdateAsync(
        IUnitOfWork unitOfWork,
        RecurringTransaction recurring,
        Account source,
        decimal amount,
        int? selectedGoalId,
        CancellationToken cancellationToken)
    {
        if (selectedGoalId is not > 0)
            return false;

        var goal = await unitOfWork.SavingGoals.GetByIdAsync(selectedGoalId.Value, cancellationToken);
        if (goal is null)
            return false;

        var goalUpdateTag = await ResolvePaymentTagAsync(unitOfWork, cancellationToken);
        if (goalUpdateTag is null)
            return false;

        var expense = new Transaction
        {
            Type = TransactionType.Expense,
            Name = recurring.Name,
            Amount = amount,
            OccurredOn = DateTime.Now,
            Notes = recurring.Name,
            ExpenseCategory = ExpenseCategory.Savings,
            SourceAccountId = source.Id,
            TagId = goalUpdateTag.Id
        };
        await unitOfWork.Transactions.AddAsync(expense, cancellationToken);
        goal.CurrentAmount += amount;
        unitOfWork.SavingGoals.Update(goal);
        ApplyExpenseToAccount(source, amount);
        unitOfWork.Accounts.Update(source);
        return true;
    }

    private static async Task<Tag?> ResolvePaymentTagAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        var tags = await unitOfWork.Tags.GetAllAsync(cancellationToken);
        return tags
            .OrderByDescending(tag => string.Equals(tag.Name, "Transfer", StringComparison.OrdinalIgnoreCase))
            .ThenBy(tag => tag.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static async Task<Tag?> ResolveBalanceUpdateTagAsync(
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var tags = await unitOfWork.Tags.GetAllAsync(cancellationToken);
        return tags.FirstOrDefault(tag =>
            string.Equals(tag.Name, "Balance Update", StringComparison.OrdinalIgnoreCase));
    }

    private static void ApplyExpenseToAccount(Account account, decimal amount)
    {
        if (account.AccountType == AccountType.Credit)
        {
            account.SpentAmount += amount;
            return;
        }

        account.Balance -= amount;
    }

    private static void ApplyIncomeToAccount(Account account, decimal amount)
    {
        if (account.AccountType == AccountType.Credit)
        {
            account.SpentAmount = Math.Max(0m, account.SpentAmount - amount);
            return;
        }

        account.Balance += amount;
    }
}
