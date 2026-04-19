using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using AutoMapper;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Constants;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Core.Interfaces.Services;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Helpers;
using Fluxo.ViewModels.Messages;
using Fluxo.ViewModels.Notifications;

namespace Fluxo.ViewModels.Shell;

public partial class NotificationPanelVM : ObservableRecipient,
    IRecipient<DateRangeSelectionChangedMessage>,
    IRecipient<AllTimeViewModeMessage>,
    IRecipient<DashboardDataInvalidatedMessage>
{
    private readonly IExpenseLogService _expenseLogService;
    private readonly IExpenseService _expenseService;
    private readonly IMapper _mapper;
    private readonly SemaphoreSlim _reloadGate = new(1, 1);
    private readonly ISpendingSourceService _spendingSourceService;
    private readonly IUserSettingsRepository _userSettingsRepository;
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
        IMapper mapper,
        IMessenger? messenger = null)
        : base(messenger ?? WeakReferenceMessenger.Default)
    {
        _expenseService = expenseService;
        _expenseLogService = expenseLogService;
        _spendingSourceService = spendingSourceService;
        _userSettingsRepository = userSettingsRepository;
        _mapper = mapper;

        Notifications.CollectionChanged += OnNotificationsCollectionChanged;
        IsActive = true;
    }

    [ObservableProperty]
    private int _notificationCount;

    [ObservableProperty]
    private bool _hasNotifications;

    public ObservableCollection<NotificationItemVM> Notifications { get; } = [];

    [RelayCommand]
    private void ClearAllNotifications()
    {
        Notifications.Clear();
    }

    public void Receive(DateRangeSelectionChangedMessage message)
    {
        _selectedRange = message.Value;
        RefreshNotifications();
    }

    public void Receive(AllTimeViewModeMessage message)
    {
        _selectedRange = null;
        RefreshNotifications();
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

        RefreshNotifications();
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

    private void RefreshNotifications()
    {
        ReplaceSystemNotifications(EvaluateSystemNotifications());
    }

    private IReadOnlyList<NotificationItemVM> EvaluateSystemNotifications()
    {
        var notifications = new List<NotificationItemVM>();

        notifications.AddRange(GetUpcomingCreditDeadlineNotifications());
        notifications.AddRange(GetUpcomingRecurringPaymentNotifications());
        notifications.AddRange(GetAutoExpenseNotifications());
        notifications.AddRange(GetBudgetThresholdNotifications());
        notifications.AddRange(GetCreditThresholdNotifications());
        notifications.AddRange(GetLowAccountNotifications());

        return notifications;
    }

    private IEnumerable<NotificationItemVM> GetUpcomingCreditDeadlineNotifications()
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
            if ((upcomingDueDate - DateTime.Today).Days != _deadlineReminderDays)
                continue;

            yield return CreateNotification(
                $"credit-deadline-{source.Id}-{upcomingDueDate:yyyyMMdd}",
                $"{source.Name} payment is coming up",
                $"{source.Name} is due on {upcomingDueDate:MMM d}.",
                NotificationSeverity.Warning);
        }
    }

    private IEnumerable<NotificationItemVM> GetUpcomingRecurringPaymentNotifications()
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

            yield return CreateNotification(
                $"recurring-deadline-{expense.Id}-{upcomingRecurringDate:yyyyMMdd}",
                $"{expense.Name} deducts in {_deadlineReminderDays} days",
                $"{expense.Name} is scheduled for {upcomingRecurringDate:MMM d}.",
                NotificationSeverity.Warning);
        }
    }

    private IEnumerable<NotificationItemVM> GetAutoExpenseNotifications()
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

        yield return CreateNotification(
            $"auto-expenses-{DateTime.Today:yyyyMMdd}",
            "Auto-expenses processed today",
            $"{expenseSummary} reached {verb} scheduled date today.",
            NotificationSeverity.Success);
    }

    private IEnumerable<NotificationItemVM> GetBudgetThresholdNotifications()
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
            yield return CreateNotification(
                "budget-threshold-needs",
                "Needs budget is almost fully spent",
                $"Needs has reached {needsPercentage}% of its allocation.",
                NotificationSeverity.Danger);

        if (HasCrossedBudgetThreshold(wantsSpent, wantsAvailable))
            yield return CreateNotification(
                "budget-threshold-wants",
                "Wants budget is almost fully spent",
                $"Wants has reached {wantsPercentage}% of its allocation.",
                NotificationSeverity.Warning);

        if (HasCrossedBudgetThreshold(investSpent, investAvailable))
            yield return CreateNotification(
                "budget-threshold-savings",
                "Savings budget is almost fully spent",
                $"Savings has reached {investPercentage}% of its allocation.",
                NotificationSeverity.Warning);
    }

    private IEnumerable<NotificationItemVM> GetCreditThresholdNotifications()
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

            yield return CreateNotification(
                $"credit-usage-{source.Id}",
                $"{source.Name} crossed the credit threshold",
                $"{source.Name} is using {usagePercentage}% of its limit.",
                NotificationSeverity.Warning);
        }
    }

    private IEnumerable<NotificationItemVM> GetLowAccountNotifications()
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

            yield return CreateNotification(
                $"low-account-{source.Id}",
                $"{source.Name} is running low",
                $"{source.Name} is down to {balancePercentage}% of its pre-spend total.",
                NotificationSeverity.Danger);
        }
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

    private static NotificationItemVM CreateNotification(
        string key,
        string title,
        string message,
        NotificationSeverity severity)
    {
        return new NotificationItemVM
        {
            Key = key,
            Title = title,
            Message = message,
            Severity = severity,
            CreatedOn = DateTime.Now,
            IsSystemGenerated = true
        };
    }

    private void ReplaceSystemNotifications(IEnumerable<NotificationItemVM> notifications)
    {
        var incomingNotificationsByKey = notifications.ToDictionary(notification => notification.Key, StringComparer.Ordinal);

        var staleNotifications = Notifications
            .Where(notification => notification.IsSystemGenerated && !incomingNotificationsByKey.ContainsKey(notification.Key))
            .ToList();

        foreach (var notification in staleNotifications)
            Notifications.Remove(notification);

        foreach (var incomingNotification in incomingNotificationsByKey.Values)
        {
            var existingNotification = Notifications.FirstOrDefault(notification =>
                string.Equals(notification.Key, incomingNotification.Key, StringComparison.Ordinal));

            if (existingNotification is null)
            {
                Notifications.Insert(0, incomingNotification);
                continue;
            }

            UpdateNotification(existingNotification, incomingNotification);
        }

        SortNotifications();
    }

    private static void UpdateNotification(NotificationItemVM target, NotificationItemVM source)
    {
        target.Title = source.Title;
        target.Message = source.Message;
        target.Severity = source.Severity;
    }

    private void SortNotifications()
    {
        var orderedNotifications = Notifications
            .OrderByDescending(notification => notification.CreatedOn)
            .ToList();

        for (var index = 0; index < orderedNotifications.Count; index++)
        {
            var currentIndex = Notifications.IndexOf(orderedNotifications[index]);
            if (currentIndex >= 0 && currentIndex != index)
                Notifications.Move(currentIndex, index);
        }
    }

    private void OnNotificationsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        NotificationCount = Notifications.Count;
        HasNotifications = NotificationCount > 0;
    }
}
