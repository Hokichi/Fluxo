using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using AutoMapper;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Constants;
using Fluxo.Core.DTO;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Services.History;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Helpers;
using Fluxo.ViewModels.Messages;
using Fluxo.ViewModels.Notifications;

namespace Fluxo.ViewModels.Shell;

public partial class MainVM : ObservableRecipient
{
    // 1. private readonly fields
    private readonly HashSet<int> _expenseLogIdsMarkedForDeletion = [];

    private readonly List<ExpenseVM> _expenses = [];
    private readonly ObservableCollection<ExpenseLogVM> _investSource = [];
    private readonly IMapper _mapper;
    private readonly IExpenseService _expenseService;
    private readonly IExpenseLogService _expenseLogService;
    private readonly ISpendingSourceService _spendingSourceService;
    private readonly ITagService _tagService;
    private readonly ObservableCollection<ExpenseLogVM> _needsSource = [];
    private readonly IUnitOfWork _unitOfWork; // kept for SavingGoals and IncomeLogs (no service)
    private readonly IUserSettingsRepository _userSettingsRepository;
    private readonly ObservableCollection<ExpenseLogVM> _wantsSource = [];

    // 2. private fields
    private decimal _allTimeInvestSpent;

    private decimal _allTimeNeedsSpent;
    private decimal _allTimeWantsSpent;
    private decimal _budgetUsageWarningPercentage = 0.90m;
    private decimal _creditUsageWarningPercentage = 0.30m;
    private int _deadlineReminderDays = 7;
    private decimal _investThreshold = 0.2m;
    private bool _isBudgetThresholdNotifEnabled = true;
    private bool _isCreditDeadlineNotifEnabled = true;
    private bool _isFixedExpensesDeductionNotifEnabled;
    private bool _isInitialized;
    private bool _isLowAccountBalanceNotifEnabled;
    private bool _isLowCreditNotifEnabled;
    private bool _isSynchronizingTagSelections;
    private bool _suppressSavingGoalNotifications;
    private decimal _lowAccountBalancePercentage = 0.20m;
    private decimal _needsThreshold = 0.5m;
    private decimal _wantsThreshold = 0.3m;

    // 3. [ObservableProperty] private fields
    // User
    [ObservableProperty] private string _username = "User";

    // Budget Summary
    [ObservableProperty] private int _dailyAllowance;

    [ObservableProperty] private decimal _totalSpent;

    // Available
    [ObservableProperty] private decimal _needsAvailable;

    [ObservableProperty] private decimal _wantsAvailable;
    [ObservableProperty] private decimal _investAvailable;

    // Spent
    [ObservableProperty] private decimal _needsSpent;

    [ObservableProperty] private decimal _wantsSpent;
    [ObservableProperty] private decimal _investSpent;

    // Percentage
    [ObservableProperty] private int _needsPercentage;

    [ObservableProperty] private int _wantsPercentage;
    [ObservableProperty] private int _investPercentage;

    // Empty State
    [ObservableProperty] private bool _isNeedsEmpty;

    [ObservableProperty] private bool _isWantsEmpty;
    [ObservableProperty] private bool _isInvestEmpty;

    // Collection Views
    [ObservableProperty] private ICollectionView _needs = CollectionViewSource.GetDefaultView(Array.Empty<ExpenseLogVM>());

    [ObservableProperty] private ICollectionView _wants = CollectionViewSource.GetDefaultView(Array.Empty<ExpenseLogVM>());
    [ObservableProperty] private ICollectionView _invest = CollectionViewSource.GetDefaultView(Array.Empty<ExpenseLogVM>());

    // Tags
    [ObservableProperty] private ObservableCollection<ExpenseTagVM> _tags = [];

    [ObservableProperty] private ObservableCollection<ExpenseTagVM> _otherTags = [];
    [ObservableProperty] private ExpenseTagVM? _selectedTag;
    [ObservableProperty] private ExpenseTagVM? _selectedVisibleTag;
    [ObservableProperty] private ExpenseTagVM? _selectedOtherTag;

    // Spending Sources
    [ObservableProperty] private ObservableCollection<SpendingSourceVM> _spendingSources = [];

    // Notifications
    [ObservableProperty] private bool _hasNotifications;

    [ObservableProperty] private int _notificationCount;

    // Saving Goals
    [ObservableProperty] private bool _hasSavingGoals;

    // 4. public properties
    public bool IsInitialized => _isInitialized;

    public MainVM(
        IExpenseService expenseService,
        IExpenseLogService expenseLogService,
        ISpendingSourceService spendingSourceService,
        ITagService tagService,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IUserSettingsRepository userSettingsRepository)
    {
        _expenseService = expenseService;
        _expenseLogService = expenseLogService;
        _spendingSourceService = spendingSourceService;
        _tagService = tagService;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _userSettingsRepository = userSettingsRepository;

        Notifications.CollectionChanged += OnNotificationsCollectionChanged;
        SavingGoals.CollectionChanged += OnSavingGoalsCollectionChanged;

        AttachExpenseLogCollectionHandlers(_needsSource);
        AttachExpenseLogCollectionHandlers(_wantsSource);
        AttachExpenseLogCollectionHandlers(_investSource);
        WeakReferenceMessenger.Default.Register<MainVM, ExpenseDetailUpdatedMessage>(this,
            static (recipient, message) => recipient.HandleExpenseDetailUpdatedMessage(message));
        WeakReferenceMessenger.Default.Register<MainVM, UsernameChangedMessage>(this,
            static (recipient, message) => recipient.Username = message.Value);
    }

    public ObservableCollection<NotificationItemVM> Notifications { get; } = [];

    public ObservableCollection<SavingGoalVM> SavingGoals { get; } = [];

    public bool HasOtherTags => OtherTags.Count > 0;

    public bool IsSelectedTagInOtherTags => SelectedOtherTag is not null;

    public decimal TotalIncomeAmount { get; private set; }

    partial void OnSelectedTagChanged(ExpenseTagVM? value)
    {
        SynchronizeTagSelections(value);
        OnPropertyChanged(nameof(IsSelectedTagInOtherTags));
        RefreshExpenseViews();
    }

    partial void OnSelectedVisibleTagChanged(ExpenseTagVM? value)
    {
        if (_isSynchronizingTagSelections) return;

        SelectedTag = value;
    }

    partial void OnSelectedOtherTagChanged(ExpenseTagVM? value)
    {
        if (_isSynchronizingTagSelections) return;

        SelectedTag = value;
    }

    partial void OnSpendingSourcesChanged(ObservableCollection<SpendingSourceVM>? oldValue,
        ObservableCollection<SpendingSourceVM> newValue)
    {
        if (oldValue is not null)
        {
            oldValue.CollectionChanged -= OnSpendingSourcesCollectionChanged;
            DetachSpendingSourceHandlers(oldValue);
        }

        newValue.CollectionChanged += OnSpendingSourcesCollectionChanged;
        AttachSpendingSourceHandlers(newValue);

        if (_isApplyingExpenseDetailRefresh)
            return;

        if (_isInitialized)
        {
            RefreshDashboardMetrics();
            RefreshNotifications();
        }
    }

    [RelayCommand]
    private void ClearSelectedTag()
    {
        SelectedTag = null;
    }

    [RelayCommand]
    private async Task DeleteExpenseLog(ExpenseLogVM? expenseLog)
    {
        if (expenseLog is null)
            return;

        if (expenseLog.IsForDeletion)
            return;

        await _expenseLogService.DeleteAsync(expenseLog.Id);

        ApplyDeletedExpenseLogToUi(expenseLog);
        WeakReferenceMessenger.Default.Send(new RecordLogMemoryMessage(
            new DeleteExpenseLogMemoryAction(expenseLog.Id)));
    }

    internal IReadOnlyList<ExpenseLogVM> GetAllExpenseLogs()
    {
        return _needsSource.Concat(_wantsSource).Concat(_investSource).ToList();
    }

    internal IReadOnlyCollection<int> GetExpenseLogIdsMarkedForDeletion()
    {
        return _expenseLogIdsMarkedForDeletion.ToArray();
    }

    internal async Task ReloadCurrentDataAsync(bool suppressGoalNotifications = false)
    {
        var spendingSources = _mapper.Map<IReadOnlyList<SpendingSourceVM>>(
            await _spendingSourceService.GetAllAsync());
        var expenses = _mapper.Map<IReadOnlyList<ExpenseVM>>(
            await _expenseService.GetAllAsync());
        var allExpenseLogs = _mapper.Map<IReadOnlyList<ExpenseLogVM>>(
            await _expenseLogService.GetAllAsync());
        // SavingGoals: no service — two-step map: Entity → DTO → VM
        var savingGoalDtos = _mapper.Map<IReadOnlyList<SavingGoalDto>>(
            await _unitOfWork.SavingGoals.GetAllAsync());
        var savingGoals = _mapper.Map<IReadOnlyList<SavingGoalVM>>(savingGoalDtos);
        var allTags = _mapper.Map<IReadOnlyList<ExpenseTagVM>>(
            await _tagService.GetAllAsync());

        LoadExpenses(expenses);
        CacheAllTimeExpenseTotals(allExpenseLogs);
        _suppressSavingGoalNotifications = suppressGoalNotifications;
        try
        {
            LoadSavingGoals(savingGoals);
        }
        finally
        {
            _suppressSavingGoalNotifications = false;
        }
        LoadTags(allTags);

        await LoadAllTimeData(spendingSources);

        RefreshExpenseViews();
        RefreshNotifications();
    }

    public async Task Initialize()
    {
        await LoadUserSettingsAsync();

        var spendingSources = _mapper.Map<IReadOnlyList<SpendingSourceVM>>(
            await _spendingSourceService.GetAllAsync());
        var expenses = _mapper.Map<IReadOnlyList<ExpenseVM>>(
            await _expenseService.GetAllAsync());
        var allExpenseLogs = _mapper.Map<IReadOnlyList<ExpenseLogVM>>(
            await _expenseLogService.GetAllAsync());
        // IncomeLogs: no service — two-step map: Entity → DTO → VM
        var incomeLogDtos = _mapper.Map<IReadOnlyList<IncomeLogDto>>(
            await _unitOfWork.IncomeLogs.GetAllAsync());
        var allIncomeLogs = _mapper.Map<IReadOnlyList<IncomeLogVM>>(incomeLogDtos);
        // SavingGoals: no service — two-step map: Entity → DTO → VM
        var savingGoalDtos = _mapper.Map<IReadOnlyList<SavingGoalDto>>(
            await _unitOfWork.SavingGoals.GetAllAsync());
        var savingGoals = _mapper.Map<IReadOnlyList<SavingGoalVM>>(savingGoalDtos);
        var allTags = _mapper.Map<IReadOnlyList<ExpenseTagVM>>(
            await _tagService.GetAllAsync());

        LoadExpenses(expenses);
        CacheAllTimeExpenseTotals(allExpenseLogs);
        LoadSpendingSources(spendingSources, allIncomeLogs, allExpenseLogs);
        LoadExpenseLogs(allExpenseLogs);
        LoadSavingGoals(savingGoals);
        LoadTags(allTags);
        ConfigureExpenseViews();
        RefreshDashboardMetrics();

        _isInitialized = true;
        RefreshExpenseViews();
        RefreshNotifications();
    }

    private async Task LoadAllTimeData(IEnumerable<SpendingSourceVM>? spendingSources = null)
    {
        var allExpenseLogs = _mapper.Map<IReadOnlyList<ExpenseLogVM>>(
            await _expenseLogService.GetAllAsync());
        var incomeLogDtos = _mapper.Map<IReadOnlyList<IncomeLogDto>>(
            await _unitOfWork.IncomeLogs.GetAllAsync());
        var allIncomeLogs = _mapper.Map<IReadOnlyList<IncomeLogVM>>(incomeLogDtos);

        LoadSpendingSources(spendingSources ?? SpendingSources, allIncomeLogs, allExpenseLogs);
        LoadExpenseLogs(allExpenseLogs);
        RefreshDashboardMetrics();
    }

    private void LoadExpenses(IEnumerable<ExpenseVM> expenses)
    {
        _expenses.Clear();
        _expenses.AddRange(expenses);
    }

    private void LoadSpendingSources(
        IEnumerable<SpendingSourceVM> spendingSources,
        IEnumerable<IncomeLogVM> incomeLogs,
        IEnumerable<ExpenseLogVM> expenseLogs)
    {
        var activeExpenseLogs = expenseLogs
            .Where(log => !log.IsForDeletion)
            .ToList();

        var moneyInBySourceId = incomeLogs
            .GroupBy(log => log.SpendingSource.Id)
            .ToDictionary(group => group.Key, group => group.Sum(log => log.Amount));

        var moneyOutBySourceId = activeExpenseLogs
            .GroupBy(log => log.SpendingSource.Id)
            .ToDictionary(group => group.Key, group => group.Sum(log => log.Amount));

        RunBatchedExpenseRefresh(() =>
            SpendingSources = new ObservableCollection<SpendingSourceVM>(spendingSources.Select(source =>
            {
                source.MoneyIn = moneyInBySourceId.GetValueOrDefault(source.Id);
                source.MoneyOut = moneyOutBySourceId.GetValueOrDefault(source.Id);
                return source;
            })));
    }

    private void CacheAllTimeExpenseTotals(IEnumerable<ExpenseLogVM> allExpenseLogs)
    {
        var activeExpenseLogs = allExpenseLogs
            .Where(log => !log.IsForDeletion)
            .ToList();

        _allTimeNeedsSpent = activeExpenseLogs
            .Where(log => log.Expense?.ExpenseCategory == ExpenseCategory.Needs)
            .Sum(log => log.Amount);
        _allTimeWantsSpent = activeExpenseLogs
            .Where(log => log.Expense?.ExpenseCategory == ExpenseCategory.Wants)
            .Sum(log => log.Amount);
        _allTimeInvestSpent = activeExpenseLogs
            .Where(log => log.Expense?.ExpenseCategory == ExpenseCategory.Savings)
            .Sum(log => log.Amount);
    }

    private void LoadExpenseLogs(IEnumerable<ExpenseLogVM> expenseLogs)
    {
        var activeExpenseLogs = expenseLogs
            .Where(log => !log.IsForDeletion)
            .ToList();

        RunBatchedExpenseRefresh(() =>
        {
            ReplaceExpenseLogs(_needsSource,
                activeExpenseLogs.Where(log => log.Expense?.ExpenseCategory == ExpenseCategory.Needs));
            ReplaceExpenseLogs(_wantsSource,
                activeExpenseLogs.Where(log => log.Expense?.ExpenseCategory == ExpenseCategory.Wants));
            ReplaceExpenseLogs(_investSource,
                activeExpenseLogs.Where(log => log.Expense?.ExpenseCategory == ExpenseCategory.Savings));
        });
    }

    private void LoadSavingGoals(IEnumerable<SavingGoalVM> savingGoals)
    {
        SavingGoals.Clear();

        foreach (var goal in savingGoals.Where(goal => goal.ProgressRatio < 1m))
            SavingGoals.Add(goal);

        HasSavingGoals = SavingGoals.Count > 0;
    }

    private void LoadTags(IEnumerable<ExpenseTagVM> allTags)
    {
        Tags = new ObservableCollection<ExpenseTagVM>(allTags.Take(5));
        OtherTags = new ObservableCollection<ExpenseTagVM>(allTags.Skip(5));
        SynchronizeTagSelections(SelectedTag);
        OnPropertyChanged(nameof(HasOtherTags));
        OnPropertyChanged(nameof(IsSelectedTagInOtherTags));
    }

    private void ConfigureExpenseViews()
    {
        Needs = CollectionViewSource.GetDefaultView(_needsSource);
        Wants = CollectionViewSource.GetDefaultView(_wantsSource);
        Invest = CollectionViewSource.GetDefaultView(_investSource);

        Needs.Filter = FilterBySelectedTag;
        Wants.Filter = FilterBySelectedTag;
        Invest.Filter = FilterBySelectedTag;
    }

    private void RefreshDashboardMetrics()
    {
        TotalIncomeAmount = SpendingSources.Sum(source => source.Balance);
        OnPropertyChanged(nameof(TotalIncomeAmount));

        NeedsAvailable = decimal.Round(TotalIncomeAmount * _needsThreshold, 2);
        WantsAvailable = decimal.Round(TotalIncomeAmount * _wantsThreshold, 2);
        InvestAvailable = decimal.Round(TotalIncomeAmount * _investThreshold, 2);

        NeedsSpent = _allTimeNeedsSpent;
        WantsSpent = _allTimeWantsSpent;
        InvestSpent = _allTimeInvestSpent;
        TotalSpent = NeedsSpent + WantsSpent + InvestSpent;

        NeedsPercentage = CalculatePercentage(NeedsSpent, NeedsAvailable);
        WantsPercentage = CalculatePercentage(WantsSpent, WantsAvailable);
        InvestPercentage = CalculatePercentage(InvestSpent, InvestAvailable);
        DailyAllowance = CalculateDailyAllowance();

        RefreshExpenseViews();
    }

    private int CalculateDailyAllowance()
    {
        var daysLeft = Math.Max(1,
            DateTime.DaysInMonth(DateTime.Today.Year, DateTime.Today.Month) - DateTime.Today.Day);
        return (int)((TotalIncomeAmount * (1 - _investThreshold) - TotalSpent) / daysLeft);
    }

    private static int CalculatePercentage(decimal spentAmount, decimal availableAmount)
    {
        if (availableAmount <= 0)
            return 0;

        return (int)Math.Round(spentAmount / availableAmount * 100, MidpointRounding.AwayFromZero);
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

        foreach (var source in SpendingSources.Where(source =>
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
                NotificationSeverity.Warning,
                true);
        }
    }

    private IEnumerable<NotificationItemVM> GetUpcomingRecurringPaymentNotifications()
    {
        if (!_isFixedExpensesDeductionNotifEnabled)
            yield break;

        foreach (var expense in _expenses.Where(expense =>
                     expense.IsActive &&
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
                NotificationSeverity.Warning,
                true);
        }
    }

    private IEnumerable<NotificationItemVM> GetAutoExpenseNotifications()
    {
        if (!_isFixedExpensesDeductionNotifEnabled)
            yield break;

        var autoExpensesDueToday = _expenses
            .Where(expense =>
                expense.IsActive &&
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
            NotificationSeverity.Success,
            true);
    }

    private IEnumerable<NotificationItemVM> GetBudgetThresholdNotifications()
    {
        if (!_isBudgetThresholdNotifEnabled)
            yield break;

        if (HasCrossedBudgetThreshold(NeedsSpent, NeedsAvailable))
            yield return CreateNotification(
                "budget-threshold-needs",
                "Needs budget is almost fully spent",
                $"Needs has reached {NeedsPercentage}% of its allocation.",
                NotificationSeverity.Danger,
                true);

        if (HasCrossedBudgetThreshold(WantsSpent, WantsAvailable))
            yield return CreateNotification(
                "budget-threshold-wants",
                "Wants budget is almost fully spent",
                $"Wants has reached {WantsPercentage}% of its allocation.",
                NotificationSeverity.Warning,
                true);

        if (HasCrossedBudgetThreshold(InvestSpent, InvestAvailable))
            yield return CreateNotification(
                "budget-threshold-savings",
                "Savings budget is almost fully spent",
                $"Savings has reached {InvestPercentage}% of its allocation.",
                NotificationSeverity.Warning,
                true);
    }

    private IEnumerable<NotificationItemVM> GetCreditThresholdNotifications()
    {
        if (!_isLowCreditNotifEnabled)
            yield break;

        foreach (var source in SpendingSources.Where(source =>
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
                NotificationSeverity.Warning,
                true);
        }
    }

    private IEnumerable<NotificationItemVM> GetLowAccountNotifications()
    {
        if (!_isLowAccountBalanceNotifEnabled)
            yield break;

        foreach (var source in SpendingSources.Where(source =>
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
                NotificationSeverity.Danger,
                true);
        }
    }

    private async Task LoadUserSettingsAsync()
    {
        var settings = await _userSettingsRepository.GetAllAsync();
        var settingsByName =
            settings.ToDictionary(setting => setting.Name, setting => setting.Value, StringComparer.Ordinal);

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

        Username = ParseString(settingsByName, UserSettingNames.PreferredDisplayName, "User");
    }

    private bool HasCrossedBudgetThreshold(decimal spentAmount, decimal availableAmount)
    {
        if (availableAmount <= 0)
            return false;

        return spentAmount / availableAmount >= _budgetUsageWarningPercentage;
    }

    private static decimal ParsePercentage(IReadOnlyDictionary<string, string> settings, string name,
        decimal defaultValue)
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
        if (settings.TryGetValue(name, out var value) && int.TryParse(value, out var parsedValue)) return parsedValue;

        return defaultValue;
    }

    private static bool ParseBool(IReadOnlyDictionary<string, string> settings, string name, bool defaultValue)
    {
        if (settings.TryGetValue(name, out var value) && bool.TryParse(value, out var parsedValue)) return parsedValue;

        return defaultValue;
    }

    private static string ParseString(IReadOnlyDictionary<string, string> settings, string name, string defaultValue)
    {
        if (!settings.TryGetValue(name, out var value))
            return defaultValue;

        var trimmed = (value ?? string.Empty).Trim();
        return trimmed.Length == 0 ? defaultValue : trimmed;
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
        NotificationSeverity severity,
        bool isSystemGenerated)
    {
        return new NotificationItemVM
        {
            Key = key,
            Title = title,
            Message = message,
            Severity = severity,
            CreatedOn = DateTime.Now,
            IsSystemGenerated = isSystemGenerated
        };
    }

    private void ReplaceSystemNotifications(IEnumerable<NotificationItemVM> notifications)
    {
        var incomingNotificationsByKey =
            notifications.ToDictionary(notification => notification.Key, StringComparer.Ordinal);

        var staleNotifications = Notifications
            .Where(notification =>
                notification.IsSystemGenerated && !incomingNotificationsByKey.ContainsKey(notification.Key))
            .ToList();

        foreach (var notification in staleNotifications) Notifications.Remove(notification);

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

    private void UpsertEventNotification(NotificationItemVM notification)
    {
        var existingNotification = Notifications.FirstOrDefault(existing =>
            string.Equals(existing.Key, notification.Key, StringComparison.Ordinal));

        if (existingNotification is null)
        {
            Notifications.Insert(0, notification);
        }
        else
        {
            UpdateNotification(existingNotification, notification);
            existingNotification.CreatedOn = notification.CreatedOn;
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
            if (currentIndex >= 0 && currentIndex != index) Notifications.Move(currentIndex, index);
        }
    }

    private bool FilterBySelectedTag(object item)
    {
        if (item is not ExpenseLogVM expenseLog) return false;

        if (SelectedTag is null) return true;

        return expenseLog.Expense?.ExpenseTag?.Id == SelectedTag.Id;
    }

    private void SynchronizeTagSelections(ExpenseTagVM? selectedTag)
    {
        _isSynchronizingTagSelections = true;

        try
        {
            SelectedVisibleTag = selectedTag is null
                ? null
                : Tags.FirstOrDefault(tag => tag.Id == selectedTag.Id);

            SelectedOtherTag = selectedTag is null
                ? null
                : OtherTags.FirstOrDefault(tag => tag.Id == selectedTag.Id);
        }
        finally
        {
            _isSynchronizingTagSelections = false;
        }
    }

    private void RefreshExpenseViews()
    {
        Needs.Refresh();
        Wants.Refresh();
        Invest.Refresh();

        IsNeedsEmpty = Needs.IsEmpty;
        IsWantsEmpty = Wants.IsEmpty;
        IsInvestEmpty = Invest.IsEmpty;
    }

    private void AttachExpenseLogCollectionHandlers(ObservableCollection<ExpenseLogVM> expenseLogs)
    {
        expenseLogs.CollectionChanged += OnExpenseLogCollectionChanged;
        AttachExpenseLogHandlers(expenseLogs);
    }

    private void AttachExpenseLogHandlers(IEnumerable<ExpenseLogVM> expenseLogs)
    {
        foreach (var expenseLog in expenseLogs) expenseLog.PropertyChanged += OnExpenseLogPropertyChanged;
    }

    private void DetachExpenseLogHandlers(IEnumerable<ExpenseLogVM> expenseLogs)
    {
        foreach (var expenseLog in expenseLogs) expenseLog.PropertyChanged -= OnExpenseLogPropertyChanged;
    }

    private void OnExpenseLogCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var removedExpenseLogs = e.OldItems?.OfType<ExpenseLogVM>().ToList() ?? [];
        var addedExpenseLogs = e.NewItems?.OfType<ExpenseLogVM>().ToList() ?? [];

        if (removedExpenseLogs.Count > 0)
            DetachExpenseLogHandlers(removedExpenseLogs);

        if (addedExpenseLogs.Count > 0)
            AttachExpenseLogHandlers(addedExpenseLogs);

        if (_isApplyingExpenseDetailRefresh)
            return;

        if (removedExpenseLogs.Count > 0 && _isInitialized)
            MarkExpenseLogsForDeletion(removedExpenseLogs);

        if (addedExpenseLogs.Count > 0 && _isInitialized)
            RestoreExpenseLogsFromDeletion(addedExpenseLogs);

        if (_isInitialized)
        {
            RefreshDashboardMetrics();
            RefreshNotifications();
        }
    }

    private void OnExpenseLogPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_isInitialized || _isApplyingExpenseDetailRefresh)
            return;

        RefreshDashboardMetrics();
        RefreshNotifications();
    }

    private void OnSavingGoalsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
            foreach (var goal in e.OldItems.OfType<SavingGoalVM>())
                goal.PropertyChanged -= OnSavingGoalPropertyChanged;

        if (e.NewItems is not null)
            foreach (var goal in e.NewItems.OfType<SavingGoalVM>())
            {
                goal.PropertyChanged += OnSavingGoalPropertyChanged;

                if (_isInitialized && !_suppressSavingGoalNotifications)
                    UpsertEventNotification(CreateNotification(
                        goal.Id > 0 ? $"saving-goal-created-{goal.Id}" : $"saving-goal-created-{Guid.NewGuid():N}",
                        "New saving goal created",
                        $"{goal.Name} was added to your savings goals.",
                        NotificationSeverity.Success,
                        false));
            }

        HasSavingGoals = SavingGoals.Count > 0;
    }

    private void OnSavingGoalPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_isInitialized || sender is not SavingGoalVM goal || goal.Id <= 0)
            return;

        var notification = Notifications.FirstOrDefault(item =>
            string.Equals(item.Key, $"saving-goal-created-{goal.Id}", StringComparison.Ordinal));

        if (notification is null)
            return;

        notification.Message = $"{goal.Name} was added to your savings goals.";
    }

    private void OnNotificationsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        NotificationCount = Notifications.Count;
        HasNotifications = NotificationCount > 0;
    }

    private void AttachSpendingSourceHandlers(IEnumerable<SpendingSourceVM> spendingSources)
    {
        foreach (var spendingSource in spendingSources)
            spendingSource.PropertyChanged += OnSpendingSourcePropertyChanged;
    }

    private void DetachSpendingSourceHandlers(IEnumerable<SpendingSourceVM> spendingSources)
    {
        foreach (var spendingSource in spendingSources)
            spendingSource.PropertyChanged -= OnSpendingSourcePropertyChanged;
    }

    private void OnSpendingSourcesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null) DetachSpendingSourceHandlers(e.OldItems.OfType<SpendingSourceVM>());

        if (e.NewItems is not null) AttachSpendingSourceHandlers(e.NewItems.OfType<SpendingSourceVM>());

        if (_isApplyingExpenseDetailRefresh)
            return;

        if (_isInitialized)
        {
            RefreshDashboardMetrics();
            RefreshNotifications();
        }
    }

    private void OnSpendingSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_isInitialized || _isApplyingExpenseDetailRefresh)
            return;

        RefreshDashboardMetrics();
        RefreshNotifications();
    }

    private void MarkExpenseLogsForDeletion(IEnumerable<ExpenseLogVM> expenseLogs)
    {
        foreach (var expenseLog in expenseLogs)
        {
            expenseLog.IsForDeletion = true;

            if (expenseLog.Id > 0) _expenseLogIdsMarkedForDeletion.Add(expenseLog.Id);
        }
    }

    private void RestoreExpenseLogsFromDeletion(IEnumerable<ExpenseLogVM> expenseLogs)
    {
        foreach (var expenseLog in expenseLogs.Where(log => log.IsForDeletion))
        {
            expenseLog.IsForDeletion = false;

            if (expenseLog.Id > 0) _expenseLogIdsMarkedForDeletion.Remove(expenseLog.Id);
        }
    }

    private static void RemoveExpenseLogFromCollection(ObservableCollection<ExpenseLogVM> expenseLogs,
        ExpenseLogVM expenseLog)
    {
        var existingExpenseLog = expenseLogs.FirstOrDefault(log =>
            ReferenceEquals(log, expenseLog) || (expenseLog.Id > 0 && log.Id == expenseLog.Id));

        if (existingExpenseLog is not null) expenseLogs.Remove(existingExpenseLog);
    }

    private void ApplyDeletedExpenseLogToUi(ExpenseLogVM expenseLog)
    {
        var trackedExpenseLog = FindExpenseLogInCollections(expenseLog.Id) ?? expenseLog;
        var sourceCollection = FindExpenseLogCollection(expenseLog.Id);
        if (sourceCollection is null)
            return;

        RunBatchedExpenseRefresh(() =>
        {
            trackedExpenseLog.IsForDeletion = true;
            RemoveExpenseLogFromCollection(sourceCollection, trackedExpenseLog);
        });

        if (trackedExpenseLog.Id > 0)
            _expenseLogIdsMarkedForDeletion.Add(trackedExpenseLog.Id);

        AddToAllTimeSpent(trackedExpenseLog.Expense?.ExpenseCategory ?? ExpenseCategory.Needs,
            -trackedExpenseLog.Amount);
        RefreshVisibleMoneyOutMetrics([trackedExpenseLog.SpendingSource.Id]);
        RefreshDashboardMetrics();
        RefreshNotifications();
    }

    private static void ReplaceExpenseLogs(ObservableCollection<ExpenseLogVM> target, IEnumerable<ExpenseLogVM> items)
    {
        target.Clear();

        foreach (var item in items.OrderByDescending(log => log.DeductedOn)) target.Add(item);
    }
}