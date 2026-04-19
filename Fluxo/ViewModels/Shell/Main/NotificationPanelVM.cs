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
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Core.Interfaces.Services;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Helpers;
using Fluxo.ViewModels.Messages;

namespace Fluxo.ViewModels.Shell.Main;

public partial class NotificationPanelVM : ObservableRecipient,
    IRecipient<DateRangeSelectionChangedMessage>,
    IRecipient<AllTimeViewModeMessage>,
    IRecipient<DashboardDataInvalidatedMessage>
{
    private readonly IExpenseLogService _expenseLogService;
    private readonly IExpenseService _expenseService;
    private readonly IMapper _mapper;
    private readonly SemaphoreSlim _reloadGate = new(1, 1);
    private readonly SemaphoreSlim _notificationSyncGate = new(1, 1);
    private readonly ISpendingSourceService _spendingSourceService;
    private readonly IUserSettingsRepository _userSettingsRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly HashSet<int> _hiddenFixedExpenseIds = [];

    private decimal _budgetUsageWarningPercentage = 0.90m;
    private int _deadlineReminderDays = 7;
    private bool _isBudgetThresholdNotifEnabled = true;
    private bool _isCreditDeadlineNotifEnabled = true;
    private bool _isFixedExpensesDeductionNotifEnabled;
    private bool _isLowAccountBalanceNotifEnabled;
    private bool _isLowCreditNotifEnabled;
    private decimal _lowAccountBalancePercentage = 0.20m;
    private decimal _needsThreshold = 0.5m;
    private (DateTime From, DateTime To)? _selectedRange;
    private decimal _creditUsageWarningPercentage = 0.30m;
    private decimal _investThreshold = 0.2m;
    private decimal _wantsThreshold = 0.3m;
    private IReadOnlyList<ExpenseVM> _expenses = [];
    private IReadOnlyList<ExpenseLogVM> _expenseLogs = [];
    private IReadOnlyList<SpendingSourceVM> _spendingSources = [];

    public NotificationPanelVM(
        IExpenseService expenseService,
        IExpenseLogService expenseLogService,
        ISpendingSourceService spendingSourceService,
        IUserSettingsRepository userSettingsRepository,
        INotificationRepository notificationRepository,
        IMapper mapper,
        IMessenger? messenger = null)
        : base(messenger ?? WeakReferenceMessenger.Default)
    {
        _expenseService = expenseService;
        _expenseLogService = expenseLogService;
        _spendingSourceService = spendingSourceService;
        _userSettingsRepository = userSettingsRepository;
        _notificationRepository = notificationRepository;
        _mapper = mapper;

        Notifications.CollectionChanged += OnNotificationsCollectionChanged;
        IsActive = true;
    }

    [ObservableProperty]
    private int _notificationCount;

    [ObservableProperty]
    private bool _hasNotifications;

    public ObservableCollection<NotificationVM> Notifications { get; } = [];

    [RelayCommand]
    private async Task ClearAllNotificationsAsync()
    {
        await _notificationSyncGate.WaitAsync();

        try
        {
            var activeNotifications = await _notificationRepository.GetActiveAsync();
            var hasChanges = false;

            foreach (var notification in activeNotifications.Where(n => !n.IsCleared))
            {
                notification.IsCleared = true;

                var category = GetNotificationCategory(notification.Type);
                if (category is not (NotificationCategory.UpcomingPayment or NotificationCategory.GoalDeadline or NotificationCategory.LatePayment))
                    notification.IsForDeletion = true;

                if (ShouldMarkForDeletion(notification, isActiveCondition: false))
                    notification.IsForDeletion = true;

                _notificationRepository.Update(notification);
                hasChanges = true;
            }

            if (hasChanges)
                await _notificationRepository.SaveChangesAsync();

            Notifications.Clear();
        }
        finally
        {
            _notificationSyncGate.Release();
        }
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

        _expenses = _mapper.Map<IReadOnlyList<ExpenseVM>>(
            await _expenseService.GetAllAsync(cancellationToken));
        _expenseLogs = _mapper.Map<IReadOnlyList<ExpenseLogVM>>(
            await _expenseLogService.GetAllAsync(cancellationToken))
            .Where(log => !log.IsForDeletion).ToList();
        _spendingSources = _mapper.Map<IReadOnlyList<SpendingSourceVM>>(
            await _spendingSourceService.GetAllAsync(cancellationToken));

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

        notifications.AddRange(GetUpcomingCreditDeadlineNotifications());
        notifications.AddRange(GetUpcomingRecurringPaymentNotifications());
        notifications.AddRange(GetAutoExpenseNotifications());
        notifications.AddRange(GetBudgetThresholdNotifications());
        notifications.AddRange(GetCreditThresholdNotifications());
        notifications.AddRange(GetLowAccountNotifications());

        return notifications;
    }

    private IEnumerable<NotificationCandidate> GetUpcomingCreditDeadlineNotifications()
    {
        if (!_isCreditDeadlineNotifEnabled)
            yield break;

        foreach (var source in _spendingSources.Where(source =>
                     source.SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL &&
                     source.MonthlyDueDate.HasValue))
        {
            var dueDate = MonthlyDueDateHelper.ResolveUpcomingDate(source.MonthlyDueDate, DateTime.Today);
            if (!dueDate.HasValue)
                continue;

            var upcomingDueDate = dueDate.Value.Date;
            var daysUntilDueDate = (upcomingDueDate - DateTime.Today).Days;
            if (daysUntilDueDate < 0 || daysUntilDueDate > _deadlineReminderDays)
                continue;

            yield return new NotificationCandidate(
                Type: BuildNotificationType($"UpcomingPayment-{source.Id}"),
                Header: $"Upcoming Payment - {source.Name}",
                Message: $"{source.Name} is due on {upcomingDueDate:MMM d}.",
                Severity: NotificationSeverity.Warning);
        }
    }

    private IEnumerable<NotificationCandidate> GetUpcomingRecurringPaymentNotifications()
    {
        if (!_isFixedExpensesDeductionNotifEnabled)
            yield break;

        foreach (var expense in _expenses.Where(expense =>
                     expense.IsActive &&
                     !_hiddenFixedExpenseIds.Contains(expense.Id) &&
                     expense.ExpenseKind == ExpenseKind.Fixed &&
                     expense.RecurringDate.HasValue))
        {
            var recurringDate = MonthlyDueDateHelper.ResolveUpcomingDate(expense.RecurringDate, DateTime.Today);
            if (!recurringDate.HasValue)
                continue;

            var upcomingRecurringDate = recurringDate.Value.Date;
            if ((upcomingRecurringDate - DateTime.Today).Days != _deadlineReminderDays)
                continue;

            yield return new NotificationCandidate(
                Type: BuildNotificationType($"UpcomingDeduction-{expense.Id}"),
                Header: $"Upcoming Deduction - {expense.Name}",
                Message: $"{expense.Name} is scheduled for {upcomingRecurringDate:MMM d}.",
                Severity: NotificationSeverity.Warning);
        }
    }

    private IEnumerable<NotificationCandidate> GetAutoExpenseNotifications()
    {
        if (!_isFixedExpensesDeductionNotifEnabled)
            yield break;

        var autoExpensesDueToday = _expenses
            .Where(expense =>
                expense.IsActive &&
                !_hiddenFixedExpenseIds.Contains(expense.Id) &&
                expense.ExpenseKind == ExpenseKind.Fixed &&
                MonthlyDueDateHelper.ResolveUpcomingDate(expense.RecurringDate, DateTime.Today)?.Date == DateTime.Today)
            .Select(expense => expense.Name)
            .ToList();

        if (autoExpensesDueToday.Count == 0)
            yield break;

        var expenseSummary = autoExpensesDueToday.Count == 1
            ? autoExpensesDueToday[0]
            : $"{autoExpensesDueToday.Count} recurring expenses";
        var verb = autoExpensesDueToday.Count == 1 ? "its" : "their";

        yield return new NotificationCandidate(
            Type: BuildNotificationType("AutoExpenseProcessed"),
            Header: "Auto Expense Processed - Scheduled Expenses",
            Message: $"{expenseSummary} reached {verb} scheduled date today.",
            Severity: NotificationSeverity.Success);
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
            var activePersistedNotifications = await _notificationRepository.GetActiveAsync(cancellationToken);
            var persistedByType = activePersistedNotifications
                .GroupBy(notification => notification.Type, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderByDescending(notification => notification.CreatedOn).First(),
                    StringComparer.Ordinal);

            var evaluatedByType = evaluatedNotifications
                .GroupBy(notification => notification.Type, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.First(),
                    StringComparer.Ordinal);

            var hasChanges = false;
            foreach (var persisted in persistedByType.Values)
            {
                var isActiveCondition = evaluatedByType.ContainsKey(persisted.Type);
                var shouldDelete = ShouldMarkForDeletion(persisted, isActiveCondition);
                if (persisted.IsForDeletion == shouldDelete)
                    continue;

                persisted.IsForDeletion = shouldDelete;
                _notificationRepository.Update(persisted);
                hasChanges = true;
            }

            foreach (var evaluated in evaluatedByType.Values)
            {
                if (persistedByType.TryGetValue(evaluated.Type, out var persisted))
                {
                    if (!string.Equals(persisted.Header, evaluated.Header, StringComparison.Ordinal) ||
                        !string.Equals(persisted.Message, evaluated.Message, StringComparison.Ordinal))
                    {
                        persisted.Header = evaluated.Header;
                        persisted.Message = evaluated.Message;
                        _notificationRepository.Update(persisted);
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

                await _notificationRepository.AddAsync(createdNotification, cancellationToken);
                persistedByType[evaluated.Type] = createdNotification;
                hasChanges = true;
            }

            if (hasChanges)
                await _notificationRepository.SaveChangesAsync(cancellationToken);

            return evaluatedByType.Values
                .Select(evaluated =>
                {
                    if (!persistedByType.TryGetValue(evaluated.Type, out var persisted))
                        return null;

                    if (persisted.IsCleared || persisted.IsForDeletion)
                        return null;

                    return new NotificationVM
                    {
                        Type = persisted.Type,
                        Header = persisted.Header,
                        Message = persisted.Message,
                        CreatedOn = persisted.CreatedOn,
                        Severity = evaluated.Severity,
                        IsCleared = persisted.IsCleared
                    };
                })
                .Where(notification => notification is not null)
                .Select(notification => notification!)
                .OrderByDescending(notification => notification.CreatedOn)
                .ToList();
        }
        finally
        {
            _notificationSyncGate.Release();
        }
    }

    private static bool ShouldMarkForDeletion(Notification notification, bool isActiveCondition)
    {
        var category = GetNotificationCategory(notification.Type);

        return category switch
        {
            NotificationCategory.UpcomingPayment => !isActiveCondition,
            NotificationCategory.GoalDeadline => !isActiveCondition,
            NotificationCategory.LatePayment => !isActiveCondition,
            _ => notification.IsCleared
        };
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

        if (typeToken.StartsWith("UpcomingDeduction", StringComparison.OrdinalIgnoreCase))
            return NotificationCategory.UpcomingDeduction;

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

    private static string BuildNotificationType(string typeToken)
    {
        return typeToken;
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
        var settings = await _userSettingsRepository.GetAllAsync(cancellationToken);
        var settingsByName = settings.ToDictionary(setting => setting.Name, setting => setting.Value, StringComparer.Ordinal);

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
        _isFixedExpensesDeductionNotifEnabled =
            ParseBool(settingsByName, UserSettingNames.IsFixedExpensesDeductionNotifEnabled, false);
        _isCreditDeadlineNotifEnabled =
            ParseBool(settingsByName, UserSettingNames.IsCreditDeadlineNotifEnabled, true);
        _isBudgetThresholdNotifEnabled =
            ParseBool(settingsByName, UserSettingNames.IsBudgetThresholdNotifEnabled, true);
        _isLowCreditNotifEnabled = ParseBool(settingsByName, UserSettingNames.IsLowCreditNotifEnabled, false);
        _isLowAccountBalanceNotifEnabled =
            ParseBool(settingsByName, UserSettingNames.IsLowAccountBalanceNotifEnabled, _isLowCreditNotifEnabled);

        _hiddenFixedExpenseIds.Clear();
        _hiddenFixedExpenseIds.UnionWith(ParseIdSet(settingsByName, UserSettingNames.HiddenFixedExpenseIds));
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

    private void ReplaceNotifications(IEnumerable<NotificationVM> notifications)
    {
        Notifications.Clear();

        foreach (var notification in notifications)
            Notifications.Add(notification);
    }

    private void OnNotificationsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        NotificationCount = Notifications.Count;
        HasNotifications = NotificationCount > 0;
    }

    private enum NotificationCategory
    {
        Other = 0,
        UpcomingPayment = 1,
        GoalDeadline = 2,
        LatePayment = 3,
        UpcomingDeduction = 4,
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
