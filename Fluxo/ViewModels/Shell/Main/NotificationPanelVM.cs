using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using AutoMapper;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Constants;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.DTO;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Resources.Resources.Messages;
using Fluxo.Services.Dialogs;
using Fluxo.Services.Notifications;
using Fluxo.Services.Updates;
using Fluxo.ViewModels.Popups;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Popups.Helpers;
using System.Text;
using System.Windows;

namespace Fluxo.ViewModels.Shell.Main;

public partial class NotificationPanelVM : ObservableRecipient,
    IRecipient<DateRangeSelectionChangedMessage>,
    IRecipient<AllTimeViewModeMessage>,
    IRecipient<DashboardDataInvalidatedMessage>
{
    private readonly IExpenseLogService _expenseLogService;
    private readonly IExpenseService _expenseService;
    private readonly IDataOperationRunner _dataOperationRunner;
    private readonly INotificationGroupingService _notificationGroupingService;
    private readonly INotificationActionService _notificationActionService;
    private readonly IAppUpdateInteractionService? _appUpdateInteractionService;
    private readonly IDialogService? _dialogService;
    private readonly IMapper _mapper;
    private readonly SemaphoreSlim _reloadGate = new(1, 1);
    private readonly SemaphoreSlim _notificationSyncGate = new(1, 1);
    private readonly ISpendingSourceService _spendingSourceService;
    private readonly HashSet<int> _hiddenFixedExpenseIds = [];
    private readonly HashSet<int> _hiddenSavingGoalIds = [];
    private readonly HashSet<int> _disabledSavingGoalIds = [];

    private decimal _budgetUsageWarningPercentage = 0.90m;
    private int _deadlineReminderDays = 7;
    private bool _isBudgetThresholdNotifEnabled = true;
    private bool _isRecurringTransactionNotifEnabled;
    private bool _isGoalDeadlineNotifEnabled = true;
    private bool _isLatePaymentNotifEnabled = true;
    private bool _isLowAccountBalanceNotifEnabled;
    private bool _isLowCreditNotifEnabled;
    private decimal _lowAccountBalancePercentage = 0.20m;
    private decimal _needsThreshold = 0.5m;
    private (DateTime From, DateTime To)? _selectedRange;
    private decimal _creditUsageWarningPercentage = 0.30m;
    private decimal _investThreshold = 0.2m;
    private decimal _wantsThreshold = 0.3m;
    private IReadOnlyList<RecurringTransactionVM> _recurringTransactions = [];
    private IReadOnlyList<ExpenseLogVM> _expenseLogs = [];
    private IReadOnlyList<SpendingSourceVM> _spendingSources = [];
    private IReadOnlyList<SavingGoalVM> _savingGoals = [];

    public NotificationPanelVM(
        IExpenseService expenseService,
        IExpenseLogService expenseLogService,
        ISpendingSourceService spendingSourceService,
        IDataOperationRunner dataOperationRunner,
        IMapper mapper,
        INotificationGroupingService? notificationGroupingService = null,
        INotificationActionService? notificationActionService = null,
        IDialogService? dialogService = null,
        IMessenger? messenger = null,
        IAppUpdateInteractionService? appUpdateInteractionService = null)
        : base(messenger ?? WeakReferenceMessenger.Default)
    {
        _expenseService = expenseService;
        _expenseLogService = expenseLogService;
        _spendingSourceService = spendingSourceService;
        _dataOperationRunner = dataOperationRunner;
        _mapper = mapper;
        _notificationActionService = notificationActionService ?? new NotificationActionService(dataOperationRunner);
        _appUpdateInteractionService = appUpdateInteractionService;
        _dialogService = dialogService;
        _notificationGroupingService = notificationGroupingService ?? new NotificationGroupingService();

        Notifications.CollectionChanged += OnNotificationsCollectionChanged;
        IsActive = true;
    }

    [ObservableProperty]
    private int _notificationCount;

    [ObservableProperty]
    private bool _hasNotifications;

    [ObservableProperty]
    private bool _hasMultipleNotifications;

    [ObservableProperty]
    private int _currentNotificationIndex = -1;

    [ObservableProperty]
    private NotificationVM? _currentNotification;

    [ObservableProperty]
    private NotificationItemVM? _currentNotificationItem;

    [ObservableProperty]
    private int _navigationDirection;

    public ObservableCollection<NotificationVM> Notifications { get; } = [];
    public ObservableCollection<NotificationItemVM> NotificationItems { get; } = [];
    public int NotificationStepCount => NotificationItems.Count;
    public int CurrentStepNumber => HasNotifications && CurrentNotificationIndex >= 0 ? CurrentNotificationIndex + 1 : 0;

    [RelayCommand]
    private async Task ClearAllNotificationsAsync()
    {
        await _notificationSyncGate.WaitAsync();

        try
        {
            await _dataOperationRunner.RunAsync(async (scope, ct) =>
            {
                var unitOfWork = scope.UnitOfWork;
                var activeNotifications = await unitOfWork.Notifications.GetActiveAsync(ct);
                var hasChanges = false;

                foreach (var notification in activeNotifications.Where(n => !n.IsCleared))
                {
                    notification.IsCleared = true;
                    notification.IsForDeletion = ShouldMarkForDeletion(notification, isActiveCondition: true);

                    unitOfWork.Notifications.Update(notification);
                    hasChanges = true;
                }

                if (hasChanges)
                    await unitOfWork.Notifications.SaveChangesAsync(ct);
            });

            ReplaceNotifications([]);
        }
        finally
        {
            _notificationSyncGate.Release();
        }
    }

    [RelayCommand]
    private async Task ClearNotificationGroupAsync(NotificationItemVM? card)
    {
        if (card is null || card.Notifications.Count == 0)
            return;

        await _notificationSyncGate.WaitAsync();

        try
        {
            var selectedKeys = card.Notifications
                .Select(notification => (notification.Type, notification.Message))
                .ToHashSet();

            await _dataOperationRunner.RunAsync(async (scope, ct) =>
            {
                var unitOfWork = scope.UnitOfWork;
                var persistedNotifications = await unitOfWork.Notifications.GetAllAsync(ct);
                var hasChanges = false;

                foreach (var persisted in persistedNotifications.Where(persisted =>
                             !persisted.IsCleared && selectedKeys.Contains((persisted.Type, persisted.Message))))
                {
                    persisted.IsCleared = true;
                    unitOfWork.Notifications.Update(persisted);
                    hasChanges = true;
                }

                if (hasChanges)
                    await unitOfWork.Notifications.SaveChangesAsync(ct);
            });
        }
        finally
        {
            _notificationSyncGate.Release();
        }

        await RefreshNotificationsAsync();
    }

    [RelayCommand]
    private async Task OpenNotificationActionAsync(NotificationItemVM? card)
    {
        if (card is null || !card.HasActionCta)
            return;

        switch (card.Category)
        {
            case NotificationGroupCategory.RecurringTransactionDue:
            case NotificationGroupCategory.UpcomingPayment:
            case NotificationGroupCategory.LatePayment:
                await OpenChecklistNotificationActionAsync(card);
                break;

            case NotificationGroupCategory.GoalDeadline:
                await OpenGoalDeadlineActionAsync(card);
                break;

            case NotificationGroupCategory.AppUpdate:
                await OpenAppUpdateNotificationActionAsync(card);
                break;
        }
    }

    [RelayCommand]
    private void NavigatePrevious()
    {
        NavigateByOffset(-1, slideDirection: 1);
    }

    [RelayCommand]
    private void NavigateNext()
    {
        NavigateByOffset(1, slideDirection: -1);
    }

    public void Receive(DateRangeSelectionChangedMessage message)
    {
        _selectedRange = message.Value;
        _ = RefreshNotificationsAsync();
    }

    public void Receive(AllTimeViewModeMessage message)
    {
        _selectedRange = null;
        _ = RefreshNotificationsAsync();
    }

    public void Receive(DashboardDataInvalidatedMessage message)
    {
        if (!message.Value.HasFlag(DashboardDataInvalidationScope.Notifications))
            return;

        _ = ReloadFromServicesAsync();
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await LoadUserSettingsAsync(cancellationToken);

        _recurringTransactions = _mapper.Map<IReadOnlyList<RecurringTransactionVM>>(
            _mapper.Map<IReadOnlyList<RecurringTransactionDto>>(
                await _dataOperationRunner.RunAsync(async (scope, ct) =>
                    await scope.UnitOfWork.RecurringTransactions.GetAllAsync(ct), cancellationToken)));
        _expenseLogs = _mapper.Map<IReadOnlyList<ExpenseLogVM>>(
            await _expenseLogService.GetAllAsync(cancellationToken))
            .Where(log => !log.IsForDeletion).ToList();
        _spendingSources = _mapper.Map<IReadOnlyList<SpendingSourceVM>>(
            await _spendingSourceService.GetAllAsync(cancellationToken));
        _savingGoals = _mapper.Map<IReadOnlyList<SavingGoalVM>>(
            _mapper.Map<IReadOnlyList<SavingGoalDto>>(
                await _dataOperationRunner.RunAsync(async (scope, ct) =>
                    await scope.UnitOfWork.SavingGoals.GetAllAsync(ct), cancellationToken)));

        await RefreshNotificationsAsync(cancellationToken);
    }

    private async Task ReloadFromServicesAsync()
    {
        await _reloadGate.WaitAsync();

        try
        {
            await LoadAsync();
        }
        finally
        {
            _reloadGate.Release();
        }
    }

    private async Task RefreshNotificationsAsync(CancellationToken cancellationToken = default)
    {
        var evaluatedNotifications = EvaluateSystemNotifications();
        var visibleNotifications = await SynchronizeNotificationsAsync(evaluatedNotifications, cancellationToken);
        ReplaceNotifications(visibleNotifications);
    }

    private IReadOnlyList<NotificationCandidate> EvaluateSystemNotifications()
    {
        var notifications = new List<NotificationCandidate>();

        notifications.AddRange(GetRecurringTransactionDueNotifications());
        notifications.AddRange(GetGoalDeadlineNotifications());
        notifications.AddRange(GetLatePaymentNotifications());
        notifications.AddRange(GetAutoExpenseNotifications());
        notifications.AddRange(GetBudgetThresholdNotifications());
        notifications.AddRange(GetCreditThresholdNotifications());
        notifications.AddRange(GetLowAccountNotifications());

        return notifications;
    }

    private IEnumerable<NotificationCandidate> GetRecurringTransactionDueNotifications()
    {
        if (!_isRecurringTransactionNotifEnabled)
            yield break;

        foreach (var transaction in _recurringTransactions.Where(transaction => transaction.IsEnabled))
        {
            var dueDate = ResolveRecurringTransactionDueDate(transaction, DateTime.Today);
            if (!dueDate.HasValue)
                continue;

            var upcomingDate = dueDate.Value.Date;
            var daysUntilDueDate = (upcomingDate - DateTime.Today).Days;
            if (daysUntilDueDate < 0 || daysUntilDueDate > _deadlineReminderDays)
                continue;

            yield return new NotificationCandidate(
                Type: BuildNotificationType($"RecurringTransactionDue-{transaction.Id}", upcomingDate),
                Header: $"Recurring Transaction Due - {transaction.Name}",
                Message: $"{transaction.Name} is scheduled for {upcomingDate:MMM d}.",
                Severity: NotificationSeverity.Warning);
        }
    }

    internal static DateTime? ResolveRecurringTransactionDueDate(RecurringTransactionVM transaction, DateTime today)
    {
        return transaction.RecurringPeriod switch
        {
            RecurringPeriod.None => null,
            RecurringPeriod.Weekly or RecurringPeriod.Biweekly => ResolveUpcomingWeekday(transaction.RecurringTime, today),
            RecurringPeriod.Monthly => MonthlyDueDateHelper.ResolveUpcomingDate(transaction.RecurringTime, today),
            _ => null
        };
    }

    private static DateTime? ResolveUpcomingWeekday(int recurringTime, DateTime today)
    {
        if (recurringTime is < 1 or > 7)
            return null;

        var currentDay = today.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)today.DayOfWeek;
        var daysUntilDue = (recurringTime - currentDay + 7) % 7;
        return today.Date.AddDays(daysUntilDue);
    }

    private IEnumerable<NotificationCandidate> GetGoalDeadlineNotifications()
    {
        if (!_isGoalDeadlineNotifEnabled)
            yield break;

        foreach (var goal in _savingGoals.Where(goal =>
                     goal.TargetAmount > 0m &&
                     goal.CurrentAmount < goal.TargetAmount &&
                     !_hiddenSavingGoalIds.Contains(goal.Id) &&
                     !_disabledSavingGoalIds.Contains(goal.Id)))
        {
            if (goal.SavingEndDate is not { } goalEndDate)
                continue;

            var savingEndDate = goalEndDate.Date;
            var daysUntilDeadline = (savingEndDate - DateTime.Today).Days;
            if (daysUntilDeadline < 0 || daysUntilDeadline > _deadlineReminderDays)
                continue;

            yield return new NotificationCandidate(
                Type: BuildNotificationType($"GoalDeadline-{goal.Id}", savingEndDate),
                Header: $"Goal Deadline - {goal.Name}",
                Message: $"{goal.Name} ends on {savingEndDate:MMM d} ({daysUntilDeadline} days left).",
                Severity: daysUntilDeadline <= 1 ? NotificationSeverity.Danger : NotificationSeverity.Warning);
        }
    }

    private IEnumerable<NotificationCandidate> GetLatePaymentNotifications()
    {
        if (!_isLatePaymentNotifEnabled)
            yield break;

        foreach (var source in _spendingSources.Where(source =>
                     source.SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL &&
                     source.MonthlyDueDate.HasValue &&
                     source.SpentAmount > 0m))
        {
            var dueDate = ResolveCurrentCycleDate(source.MonthlyDueDate, DateTime.Today);
            if (!dueDate.HasValue || DateTime.Today <= dueDate.Value.Date)
                continue;

            yield return new NotificationCandidate(
                Type: BuildNotificationType($"LatePayment-{source.Id}", dueDate.Value.Date),
                Header: $"Late Payment - {source.Name}",
                Message: $"{source.Name} payment due on {dueDate.Value:MMM d} is overdue.",
                Severity: NotificationSeverity.Danger);
        }
    }

    private IEnumerable<NotificationCandidate> GetAutoExpenseNotifications()
    {
        yield break;
    }

    private IEnumerable<NotificationCandidate> GetBudgetThresholdNotifications()
    {
        if (!_isBudgetThresholdNotifEnabled)
            yield break;

        var visibleExpenseLogs = GetVisibleExpenseLogs();
        var totalIncomeAmount = _spendingSources.Sum(source => source.Balance);
        var needsAvailable = decimal.Round(totalIncomeAmount * _needsThreshold, 2);
        var wantsAvailable = decimal.Round(totalIncomeAmount * _wantsThreshold, 2);
        var investAvailable = decimal.Round(totalIncomeAmount * _investThreshold, 2);

        var needsSpent = visibleExpenseLogs
            .Where(log => log.Expense?.ExpenseCategory == ExpenseCategory.Needs)
            .Sum(log => log.Amount);
        var wantsSpent = visibleExpenseLogs
            .Where(log => log.Expense?.ExpenseCategory == ExpenseCategory.Wants)
            .Sum(log => log.Amount);
        var investSpent = visibleExpenseLogs
            .Where(log => log.Expense?.ExpenseCategory == ExpenseCategory.Savings)
            .Sum(log => log.Amount);

        var needsPercentage = CalculatePercentage(needsSpent, needsAvailable);
        var wantsPercentage = CalculatePercentage(wantsSpent, wantsAvailable);
        var investPercentage = CalculatePercentage(investSpent, investAvailable);

        if (HasCrossedBudgetThreshold(needsSpent, needsAvailable))
            yield return new NotificationCandidate(
                Type: BuildNotificationType("BudgetThresholdNeeds"),
                Header: "Budget Threshold - Needs",
                Message: $"Needs has reached {needsPercentage}% of its allocation.",
                Severity: NotificationSeverity.Danger);

        if (HasCrossedBudgetThreshold(wantsSpent, wantsAvailable))
            yield return new NotificationCandidate(
                Type: BuildNotificationType("BudgetThresholdWants"),
                Header: "Budget Threshold - Wants",
                Message: $"Wants has reached {wantsPercentage}% of its allocation.",
                Severity: NotificationSeverity.Warning);

        if (HasCrossedBudgetThreshold(investSpent, investAvailable))
            yield return new NotificationCandidate(
                Type: BuildNotificationType("BudgetThresholdSavings"),
                Header: "Budget Threshold - Savings",
                Message: $"Savings has reached {investPercentage}% of its allocation.",
                Severity: NotificationSeverity.Warning);
    }

    private IEnumerable<NotificationCandidate> GetCreditThresholdNotifications()
    {
        if (!_isLowCreditNotifEnabled)
            yield break;

        foreach (var source in _spendingSources.Where(source =>
                     source.SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL &&
                     source.AccountLimit > 0))
        {
            var creditUsage = source.SpentAmount / source.AccountLimit;
            if (creditUsage < _creditUsageWarningPercentage)
                continue;

            var usagePercentage = (int)Math.Round(creditUsage * 100, MidpointRounding.AwayFromZero);

            yield return new NotificationCandidate(
                Type: BuildNotificationType($"LowCredit-{source.Id}"),
                Header: $"Low Credit - {source.Name}",
                Message: $"{source.Name} is using {usagePercentage}% of its limit.",
                Severity: NotificationSeverity.Warning);
        }
    }

    private IEnumerable<NotificationCandidate> GetLowAccountNotifications()
    {
        if (!_isLowAccountBalanceNotifEnabled)
            yield break;

        foreach (var source in _spendingSources.Where(source =>
                     source.SpendingSourceType is SpendingSourceType.Cash or SpendingSourceType.Checking))
        {
            var currentBalance = source.Balance;
            var totalBeforeSpending = currentBalance + source.MoneyOut;

            if (totalBeforeSpending <= 0)
                continue;

            var remainingBalancePercentage = currentBalance / totalBeforeSpending;
            if (remainingBalancePercentage >= _lowAccountBalancePercentage)
                continue;

            var balancePercentage = (int)Math.Round(remainingBalancePercentage * 100, MidpointRounding.AwayFromZero);

            yield return new NotificationCandidate(
                Type: BuildNotificationType($"LowBalance-{source.Id}"),
                Header: $"Low Balance - {source.Name}",
                Message: $"{source.Name} is down to {balancePercentage}% of its pre-spend total.",
                Severity: NotificationSeverity.Danger);
        }
    }

    private async Task<IReadOnlyList<NotificationVM>> SynchronizeNotificationsAsync(
        IReadOnlyList<NotificationCandidate> evaluatedNotifications,
        CancellationToken cancellationToken)
    {
        await _notificationSyncGate.WaitAsync(cancellationToken);

        try
        {
            return await _dataOperationRunner.RunAsync(async (scope, ct) =>
            {
                var unitOfWork = scope.UnitOfWork;
                var persistedNotifications = (await unitOfWork.Notifications.GetAllAsync(ct))
                    .OrderByDescending(notification => notification.CreatedOn)
                    .ToList();
                var activeTypes = evaluatedNotifications
                    .Select(notification => notification.Type)
                    .ToHashSet(StringComparer.Ordinal);
                var evaluatedUnique = evaluatedNotifications
                    .GroupBy(notification => $"{notification.Type}\n{notification.Message}", StringComparer.Ordinal)
                    .Select(group => group.First())
                    .ToList();

                var hasChanges = false;
                foreach (var persisted in persistedNotifications)
                {
                    var isActiveCondition = activeTypes.Contains(persisted.Type);
                    var shouldDelete = ShouldMarkForDeletion(persisted, isActiveCondition);
                    if (persisted.IsForDeletion == shouldDelete)
                        continue;

                    persisted.IsForDeletion = shouldDelete;
                    unitOfWork.Notifications.Update(persisted);
                    hasChanges = true;
                }

                foreach (var evaluated in evaluatedUnique)
                {
                    var now = DateTime.Now;
                    var duplicate = persistedNotifications
                        .Where(notification =>
                            string.Equals(notification.Type, evaluated.Type, StringComparison.Ordinal) &&
                            string.Equals(notification.Message, evaluated.Message, StringComparison.Ordinal) &&
                            notification.CreatedOn <= now)
                        .OrderByDescending(notification => notification.CreatedOn)
                        .FirstOrDefault();
                    if (duplicate is not null)
                    {
                        if (!string.Equals(duplicate.Header, evaluated.Header, StringComparison.Ordinal))
                        {
                            duplicate.Header = evaluated.Header;
                            unitOfWork.Notifications.Update(duplicate);
                            hasChanges = true;
                        }

                        continue;
                    }

                    var createdNotification = new Notification
                    {
                        Type = evaluated.Type,
                        Header = evaluated.Header,
                        Message = evaluated.Message,
                        CreatedOn = DateTime.Now,
                        IsCleared = false,
                        IsForDeletion = false
                    };

                    await unitOfWork.Notifications.AddAsync(createdNotification, ct);
                    persistedNotifications.Add(createdNotification);
                    hasChanges = true;
                }

                if (hasChanges)
                    await unitOfWork.Notifications.SaveChangesAsync(ct);

                var severityByType = evaluatedUnique
                    .GroupBy(notification => notification.Type, StringComparer.Ordinal)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Last().Severity,
                        StringComparer.Ordinal);

                return persistedNotifications
                    .Where(notification => !notification.IsCleared && !notification.IsForDeletion)
                    .Select(notification => new NotificationVM
                    {
                        Type = notification.Type,
                        Header = notification.Header,
                        Message = notification.Message,
                        CreatedOn = notification.CreatedOn,
                        Severity = severityByType.TryGetValue(notification.Type, out var severity)
                            ? severity
                            : InferSeverityFromType(notification.Type),
                        IsCleared = notification.IsCleared
                    })
                    .OrderByDescending(notification => notification.CreatedOn)
                    .ToList();
            }, cancellationToken);
        }
        finally
        {
            _notificationSyncGate.Release();
        }
    }

    private bool ShouldMarkForDeletion(Notification notification, bool isActiveCondition)
    {
        var category = GetNotificationCategory(notification.Type);

        return category switch
        {
            NotificationCategory.UpcomingPayment => true,
            NotificationCategory.GoalDeadline =>
                IsDeadlinePassed(notification.Type) ||
                (!TryExtractNotificationDate(notification.Type, out _) && !isActiveCondition),
            NotificationCategory.LatePayment => IsLatePaymentProcessed(notification.Type),
            NotificationCategory.RecurringTransactionDue => IsRecurringTransactionNotificationStale(notification.Type),
            _ => notification.IsCleared
        };
    }

    private bool IsRecurringTransactionNotificationStale(string notificationType)
    {
        if (!TryExtractNotificationEntityId(notificationType, "RecurringTransactionDue-", out var recurringTransactionId))
            return true;

        var recurringTransaction = _recurringTransactions.FirstOrDefault(transaction => transaction.Id == recurringTransactionId);
        return recurringTransaction is null || !recurringTransaction.IsEnabled;
    }

    private bool IsLatePaymentProcessed(string notificationType)
    {
        if (!TryExtractNotificationEntityId(notificationType, "LatePayment-", out var sourceId))
            return false;

        var spendingSource = _spendingSources.FirstOrDefault(source => source.Id == sourceId);
        if (spendingSource is null)
            return true;

        return spendingSource.SpentAmount <= 0m;
    }

    private static bool IsDeadlinePassed(string notificationType)
    {
        if (!TryExtractNotificationDate(notificationType, out var deadline))
            return false;

        return DateTime.Today > deadline.Date;
    }

    private static bool TryExtractNotificationDate(string notificationType, out DateTime deadline)
    {
        deadline = default;
        var separatorIndex = notificationType.LastIndexOf('_');
        if (separatorIndex < 0 || separatorIndex >= notificationType.Length - 1)
            return false;

        var dateToken = notificationType[(separatorIndex + 1)..];
        return DateTime.TryParseExact(
            dateToken,
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out deadline);
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

    private static int ExtractNotificationEntityId(string notificationType, string prefix)
    {
        return TryExtractNotificationEntityId(notificationType, prefix, out var entityId) ? entityId : 0;
    }

    private static NotificationCategory GetNotificationCategory(string notificationType)
    {
        var typeToken = notificationType.Split('_')[0];

        if (typeToken.StartsWith("UpcomingPayment", StringComparison.OrdinalIgnoreCase))
            return NotificationCategory.UpcomingPayment;

        if (typeToken.StartsWith("GoalDeadline", StringComparison.OrdinalIgnoreCase))
            return NotificationCategory.GoalDeadline;

        if (typeToken.StartsWith("LatePayment", StringComparison.OrdinalIgnoreCase))
            return NotificationCategory.LatePayment;

        if (typeToken.StartsWith("RecurringTransactionDue", StringComparison.OrdinalIgnoreCase) ||
            typeToken.StartsWith("UpcomingDeduction", StringComparison.OrdinalIgnoreCase))
            return NotificationCategory.RecurringTransactionDue;

        if (typeToken.StartsWith("LowBalance", StringComparison.OrdinalIgnoreCase))
            return NotificationCategory.LowBalance;

        if (typeToken.StartsWith("LowCredit", StringComparison.OrdinalIgnoreCase))
            return NotificationCategory.LowCredit;

        if (typeToken.StartsWith("BudgetThreshold", StringComparison.OrdinalIgnoreCase))
            return NotificationCategory.BudgetThreshold;

        if (typeToken.StartsWith("AutoExpenseProcessed", StringComparison.OrdinalIgnoreCase))
            return NotificationCategory.AutoExpenseProcessed;

        return NotificationCategory.Other;
    }

    private static NotificationSeverity InferSeverityFromType(string notificationType)
    {
        return GetNotificationCategory(notificationType) switch
        {
            NotificationCategory.AutoExpenseProcessed => NotificationSeverity.Success,
            NotificationCategory.LatePayment or NotificationCategory.LowBalance => NotificationSeverity.Danger,
            NotificationCategory.UpcomingPayment or NotificationCategory.RecurringTransactionDue or NotificationCategory.GoalDeadline or
                NotificationCategory.LowCredit or NotificationCategory.BudgetThreshold => NotificationSeverity.Warning,
            _ => NotificationSeverity.Info
        };
    }

    private static string BuildNotificationType(string typeToken, DateTime? effectiveDate = null)
    {
        return effectiveDate.HasValue
            ? $"{typeToken}_{effectiveDate.Value:yyyyMMdd}"
            : typeToken;
    }

    private static DateTime? ResolveCurrentCycleDate(int? monthlyDueDate, DateTime today)
    {
        var normalizedDueDate = MonthlyDueDateHelper.Normalize(monthlyDueDate);
        if (!normalizedDueDate.HasValue)
            return null;

        return new DateTime(today.Year, today.Month, normalizedDueDate.Value);
    }

    private IReadOnlyList<ExpenseLogVM> GetVisibleExpenseLogs()
    {
        if (_selectedRange is not { } range)
            return _expenseLogs;

        return _expenseLogs
            .Where(log => log.DeductedOn.Date >= range.From.Date && log.DeductedOn.Date <= range.To.Date)
            .ToList();
    }

    private async Task LoadUserSettingsAsync(CancellationToken cancellationToken)
    {
        var settingsByName = await _dataOperationRunner.RunAsync(async (scope, ct) =>
        {
            var settings = await scope.UnitOfWork.UserSettings.GetAllAsync(ct);
            return settings.ToDictionary(setting => setting.Name, setting => setting.Value, StringComparer.Ordinal);
        }, cancellationToken);

        _needsThreshold = ParsePercentage(settingsByName, UserSettingNames.NeedsThreshold, 50m);
        _wantsThreshold = ParsePercentage(settingsByName, UserSettingNames.WantsThreshold, 30m);
        _investThreshold = ParsePercentage(settingsByName, UserSettingNames.InvestThreshold, 20m);
        _deadlineReminderDays = ParseInt(settingsByName, UserSettingNames.DeadlineReminderDays, 7);
        _budgetUsageWarningPercentage =
            ParseDecimal(settingsByName, UserSettingNames.BudgetUsageWarningPercentage, 0.90m);
        _creditUsageWarningPercentage =
            ParseDecimal(settingsByName, UserSettingNames.CreditUsageWarningPercentage, 0.30m);
        _lowAccountBalancePercentage =
            ParseDecimal(settingsByName, UserSettingNames.LowAccountBalancePercentage, 0.20m);
        _isRecurringTransactionNotifEnabled =
            ParseBool(settingsByName, UserSettingNames.IsFixedExpensesDeductionNotifEnabled, false);
        _isBudgetThresholdNotifEnabled =
            ParseBool(settingsByName, UserSettingNames.IsBudgetThresholdNotifEnabled, true);
        _isLowCreditNotifEnabled = ParseBool(settingsByName, UserSettingNames.IsLowCreditNotifEnabled, false);
        _isLowAccountBalanceNotifEnabled =
            ParseBool(settingsByName, UserSettingNames.IsLowAccountBalanceNotifEnabled, _isLowCreditNotifEnabled);
        _isGoalDeadlineNotifEnabled =
            ParseBool(settingsByName, UserSettingNames.IsGoalDeadlineNotifEnabled, true);
        _isLatePaymentNotifEnabled =
            ParseBool(settingsByName, UserSettingNames.IsLatePaymentNotifEnabled, true);

        _hiddenSavingGoalIds.Clear();
        _hiddenSavingGoalIds.UnionWith(ParseIdSet(settingsByName, UserSettingNames.HiddenSavingGoalIds));
        _disabledSavingGoalIds.Clear();
        _disabledSavingGoalIds.UnionWith(ParseIdSet(settingsByName, UserSettingNames.DisabledSavingGoalIds));
    }

    private bool HasCrossedBudgetThreshold(decimal spentAmount, decimal availableAmount)
    {
        if (availableAmount <= 0)
            return false;

        return spentAmount / availableAmount >= _budgetUsageWarningPercentage;
    }

    private static int CalculatePercentage(decimal spentAmount, decimal availableAmount)
    {
        if (availableAmount <= 0)
            return 0;

        return (int)Math.Round(spentAmount / availableAmount * 100, MidpointRounding.AwayFromZero);
    }

    private static decimal ParsePercentage(IReadOnlyDictionary<string, string> settings, string name, decimal defaultValue)
    {
        var percentageValue = ParseDecimal(settings, name, defaultValue);
        return percentageValue / 100m;
    }

    private static decimal ParseDecimal(IReadOnlyDictionary<string, string> settings, string name, decimal defaultValue)
    {
        if (settings.TryGetValue(name, out var value) &&
            decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedValue))
            return parsedValue;

        return defaultValue;
    }

    private static int ParseInt(IReadOnlyDictionary<string, string> settings, string name, int defaultValue)
    {
        if (settings.TryGetValue(name, out var value) && int.TryParse(value, out var parsedValue))
            return parsedValue;

        return defaultValue;
    }

    private static bool ParseBool(IReadOnlyDictionary<string, string> settings, string name, bool defaultValue)
    {
        if (settings.TryGetValue(name, out var value) && bool.TryParse(value, out var parsedValue))
            return parsedValue;

        return defaultValue;
    }

    private static IReadOnlyCollection<int> ParseIdSet(IReadOnlyDictionary<string, string> settings, string name)
    {
        if (!settings.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
            return [];

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
                ? id
                : -1)
            .Where(id => id > 0)
            .Distinct()
            .ToArray();
    }

    private async Task OpenChecklistNotificationActionAsync(NotificationItemVM card)
    {
        if (_dialogService is null)
            return;

        var prefix = card.Category switch
        {
            NotificationGroupCategory.RecurringTransactionDue => "RecurringTransactionDue-",
            NotificationGroupCategory.UpcomingPayment => "UpcomingPayment-",
            NotificationGroupCategory.LatePayment => "LatePayment-",
            _ => string.Empty
        };

        if (prefix.Length == 0)
            return;

        var isRecurringDue = card.Category == NotificationGroupCategory.RecurringTransactionDue;
        var availableSources = _spendingSources
            .Where(source => source.IsEnabled)
            .OrderBy(source => source.SpendingSourceType)
            .ThenBy(source => source.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var tagVm = _mapper.Map<IReadOnlyList<ExpenseTagVM>>(
            _mapper.Map<IReadOnlyList<ExpenseTagDto>>(
                await _dataOperationRunner.RunAsync(async (scope, ct) =>
                    await scope.UnitOfWork.ExpenseTags.GetAllAsync(ct))));

        var items = card.Notifications
            .Select(notification => new
            {
                Notification = notification,
                EntityId = ExtractNotificationEntityId(notification.Type, prefix)
            })
            .Where(item => item.EntityId > 0)
            .GroupBy(item => item.EntityId)
            .Select(group => group.OrderByDescending(item => item.Notification.CreatedOn).First())
            .Select(item =>
            {
                var checklistItem = new NotificationChecklistActionItemVM
                {
                    EntityId = item.EntityId,
                    Label = item.Notification.Header
                };

                if (!isRecurringDue)
                    return checklistItem;

                checklistItem.RequiresSourceSelection = true;
                var recurring = _recurringTransactions.FirstOrDefault(transaction => transaction.Id == item.EntityId);
                checklistItem.RecurringTransactionType = recurring?.Type;
                checklistItem.Amount = recurring?.Amount ?? 0m;
                checklistItem.OriginalAmount = checklistItem.Amount;
                foreach (var source in availableSources)
                    checklistItem.AvailableSources.Add(source);
                foreach (var tag in tagVm)
                    checklistItem.AvailableTags.Add(tag);
                foreach (var goal in _savingGoals)
                    checklistItem.AvailableGoals.Add(goal);
                checklistItem.SelectedSourceId = recurring?.Source?.Id ?? availableSources.FirstOrDefault()?.Id;
                checklistItem.SelectedTagId = recurring?.Tag?.Id;
                checklistItem.SelectedGoalId = recurring?.Goal?.Id;

                return checklistItem;
            })
            .OrderBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (items.Count == 0)
            return;

        var viewModel = new NotificationChecklistActionVM(items);
        viewModel.ProcessAsyncCallback = async () =>
        {
            foreach (var item in viewModel.Items.Where(item => item.ShouldAskToUpdateAmount))
            {
                var choice = _dialogService.ShowQuestion(
                    $"Update the saved recurring amount for {item.Label} to {item.Amount:N2}?",
                    "Update Recurring Amount");
                item.UpdateRecurringAmount = choice == MessageBoxResult.Yes;
            }

            var decisions = viewModel.ActionDecisions
                .Distinct()
                .ToArray();

            if (decisions.Length == 0)
                return false;

            if (!await _notificationActionService.ExecuteChecklistActionAsync(card, decisions))
                return false;

            await RefreshNotificationsAsync();
            return true;
        };

        _dialogService.ShowNotificationChecklistAction(viewModel);
    }

    private async Task OpenGoalDeadlineActionAsync(NotificationItemVM card)
    {
        if (_dialogService is null)
            return;

        var goalIds = card.Notifications
            .Select(notification => ExtractNotificationEntityId(notification.Type, "GoalDeadline-"))
            .Where(entityId => entityId > 0)
            .Distinct()
            .ToArray();

        if (goalIds.Length == 0)
            return;

        var remainingAmount = goalIds
            .Select(goalId => _savingGoals.FirstOrDefault(goal => goal.Id == goalId))
            .Where(goal => goal is not null)
            .Select(goal => Math.Max(goal!.TargetAmount - goal.CurrentAmount, 0m))
            .DefaultIfEmpty(0m)
            .Max();

        var eligibleSources = _spendingSources
            .Where(source => source.SpendingSourceType is SpendingSourceType.Cash or SpendingSourceType.Checking)
            .ToList();

        var viewModel = new GoalDeadlineActionVM(eligibleSources)
        {
            RemainingAmount = remainingAmount,
            EnteredAmount = remainingAmount
        };

        var dialogResult = _dialogService.ShowGoalDeadlineAction(viewModel);
        if (dialogResult != true && viewModel.SelectedAction == GoalDeadlineActionType.None)
            return;

        if (viewModel.SelectedAction == GoalDeadlineActionType.None)
            return;

        if (await _notificationActionService.ExecuteGoalActionAsync(card, viewModel.SelectedAction))
            await RefreshNotificationsAsync();
    }

    private async Task OpenAppUpdateNotificationActionAsync(NotificationItemVM card)
    {
        if (_appUpdateInteractionService is null || card.Notifications.Count == 0)
            return;

        var newestNotification = card.Notifications
            .OrderByDescending(notification => notification.CreatedOn)
            .FirstOrDefault();
        if (newestNotification is null
            || !TryParseAppUpdateCheckResult(newestNotification.Type, out var parsedUpdate))
        {
            return;
        }

        await _appUpdateInteractionService.HandleAvailableUpdateAsync(parsedUpdate, owner: null);
    }

    private static bool TryParseAppUpdateCheckResult(string notificationType, out AppUpdateCheckResult update)
    {
        update = AppUpdateCheckResult.Error("Unable to parse app update notification payload.");

        const string appUpdatePrefix = "AppUpdate-";
        if (string.IsNullOrWhiteSpace(notificationType)
            || !notificationType.StartsWith(appUpdatePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var payload = notificationType[appUpdatePrefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(payload))
            return false;

        if (TryParseMetadataPayload(payload, out update))
            return true;

        if (!Version.TryParse(payload, out _))
            return false;

        update = AppUpdateCheckResult.UpdateAvailable(
            payload,
            string.Empty,
            string.Empty);
        return true;
    }

    private static bool TryParseMetadataPayload(string payload, out AppUpdateCheckResult update)
    {
        update = AppUpdateCheckResult.Error("Unable to parse app update notification metadata payload.");

        var parts = payload.Split('.', StringSplitOptions.None);
        if (parts.Length != 3)
            return false;

        if (!TryDecodeToken(parts[0], out var latestVersion)
            || !TryDecodeToken(parts[1], out var installerAssetName)
            || !TryDecodeToken(parts[2], out var installerDownloadUrl)
            || string.IsNullOrWhiteSpace(latestVersion)
            || string.IsNullOrWhiteSpace(installerAssetName)
            || string.IsNullOrWhiteSpace(installerDownloadUrl)
            || !Uri.TryCreate(installerDownloadUrl.Trim(), UriKind.Absolute, out _))
        {
            return false;
        }

        update = AppUpdateCheckResult.UpdateAvailable(
            latestVersion.Trim(),
            installerAssetName.Trim(),
            installerDownloadUrl.Trim());
        return true;
    }

    private static bool TryDecodeToken(string encodedToken, out string value)
    {
        value = string.Empty;
        if (string.IsNullOrWhiteSpace(encodedToken))
            return false;

        try
        {
            var base64 = encodedToken
                .Replace('-', '+')
                .Replace('_', '/');
            var padding = base64.Length % 4;
            if (padding > 0)
            {
                base64 = base64.PadRight(base64.Length + (4 - padding), '=');
            }

            var bytes = Convert.FromBase64String(base64);
            value = Encoding.UTF8.GetString(bytes);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private void ReplaceNotifications(IEnumerable<NotificationVM> notifications)
    {
        Notifications.Clear();

        foreach (var notification in notifications)
            Notifications.Add(notification);

        var groupedNotifications = _notificationGroupingService.Group(Notifications.ToList());
        NotificationItems.Clear();

        foreach (var groupedNotification in groupedNotifications)
            NotificationItems.Add(groupedNotification);

        SyncCurrentNotificationSelection();
    }

    private void OnNotificationsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        NotificationCount = Notifications.Count;
        HasNotifications = NotificationCount > 0;
        SyncCurrentNotificationSelection();
    }

    partial void OnHasNotificationsChanged(bool value)
    {
        OnPropertyChanged(nameof(CurrentStepNumber));
    }

    partial void OnCurrentNotificationIndexChanged(int value)
    {
        OnPropertyChanged(nameof(CurrentStepNumber));
    }

    private void NavigateByOffset(int offset, int slideDirection)
    {
        if (NotificationItems.Count == 0)
            return;

        if (NotificationItems.Count == 1)
        {
            SetCurrentNotificationByIndex(0, animateDirection: 0);
            return;
        }

        var normalizedCurrentIndex = CurrentNotificationIndex >= 0 ? CurrentNotificationIndex : 0;
        var targetIndex = (normalizedCurrentIndex + offset + NotificationItems.Count) % NotificationItems.Count;
        SetCurrentNotificationByIndex(targetIndex, slideDirection);
    }

    private void SyncCurrentNotificationSelection()
    {
        OnPropertyChanged(nameof(NotificationStepCount));
        HasMultipleNotifications = NotificationItems.Count > 1;

        if (NotificationItems.Count == 0)
        {
            CurrentNotificationIndex = -1;
            CurrentNotificationItem = null;
            CurrentNotification = null;
            NavigationDirection = 0;
            return;
        }

        var existingIndex = CurrentNotificationItem is null ? -1 : NotificationItems.IndexOf(CurrentNotificationItem);
        if (existingIndex >= 0)
        {
            CurrentNotificationIndex = existingIndex;
            NavigationDirection = 0;
            return;
        }

        var clampedIndex = CurrentNotificationIndex >= 0 && CurrentNotificationIndex < NotificationItems.Count
            ? CurrentNotificationIndex
            : 0;

        SetCurrentNotificationByIndex(clampedIndex, animateDirection: 0);
    }

    private void SetCurrentNotificationByIndex(int index, int animateDirection)
    {
        if (NotificationItems.Count == 0)
        {
            CurrentNotificationIndex = -1;
            CurrentNotificationItem = null;
            CurrentNotification = null;
            NavigationDirection = 0;
            return;
        }

        if (index < 0 || index >= NotificationItems.Count)
            index = 0;

        CurrentNotificationIndex = index;
        NavigationDirection = animateDirection;
        CurrentNotificationItem = NotificationItems[index];
        CurrentNotification = CurrentNotificationItem.Notifications.FirstOrDefault();
    }

    private enum NotificationCategory
    {
        Other = 0,
        UpcomingPayment = 1,
        GoalDeadline = 2,
        LatePayment = 3,
        RecurringTransactionDue = 4,
        LowBalance = 5,
        LowCredit = 6,
        BudgetThreshold = 7,
        AutoExpenseProcessed = 8
    }

    private readonly record struct NotificationCandidate(
        string Type,
        string Header,
        string Message,
        NotificationSeverity Severity);
}
