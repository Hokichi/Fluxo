using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxo.Core.Constants;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.ViewModels.Controls;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Notifications;

namespace Fluxo.ViewModels.Shell;

public partial class MainVM : ObservableRecipient
{
    private readonly HashSet<int> _expenseLogIdsMarkedForDeletion = [];
    private readonly List<ExpenseVM> _expenses = [];
    private readonly ObservableCollection<ExpenseLogVM> _investSource = [];

    private readonly ObservableCollection<ExpenseLogVM> _needsSource = [];

    private readonly IViewModelReadUnitOfWork<ExpenseVM, ExpenseLogVM, IncomeLogVM, ExpenseTagVM, SavingGoalVM, SpendingSourceVM>
        _readUnitOfWork;

    private readonly IUserSettingsRepository _userSettingsRepository;
    private readonly ObservableCollection<ExpenseLogVM> _wantsSource = [];
    private decimal _budgetUsageWarningPercentage = 0.90m;
    private decimal _creditUsageWarningPercentage = 0.30m;

    [ObservableProperty] private int _dailyAllowance;

    [ObservableProperty] private ObservableCollection<DayOfWeekVM> _daysOfWeek = [];
    private int _deadlineReminderDays = 7;

    [ObservableProperty] private bool _hasNotifications;
    [ObservableProperty] private bool _hasSavingGoals;

    [ObservableProperty]
    private ICollectionView _invest = CollectionViewSource.GetDefaultView(Array.Empty<ExpenseLogVM>());

    [ObservableProperty] private decimal _investAvailable;
    [ObservableProperty] private int _investPercentage;
    [ObservableProperty] private decimal _investSpent;
    private decimal _investThreshold = 0.2m;
    private bool _isFixedExpensesDeductionNotifEnabled;
    private bool _isInitialized;
    [ObservableProperty] private bool _isInvestEmpty;
    private bool _isLowCreditNotifEnabled;

    [ObservableProperty] private bool _isNeedsEmpty;
    [ObservableProperty] private bool _isNotificationPanelOpen;

    private bool _isSynchronizingTagSelections;
    [ObservableProperty] private bool _isWantsEmpty;
    [ObservableProperty] private bool _canNavigateForward;
    private DateTime _lastSelectedDate = DateTime.Today;
    private int _spinnerPageOffset;
    private decimal _lowAccountBalancePercentage = 0.20m;

    [ObservableProperty]
    private ICollectionView _needs = CollectionViewSource.GetDefaultView(Array.Empty<ExpenseLogVM>());

    [ObservableProperty] private decimal _needsAvailable;

    [ObservableProperty] private int _needsPercentage;

    [ObservableProperty] private decimal _needsSpent;
    private decimal _needsThreshold = 0.5m;
    [ObservableProperty] private int _notificationCount;
    [ObservableProperty] private ObservableCollection<ExpenseTagVM> _otherTags = [];
    [ObservableProperty] private DayOfWeekVM _selectedDay = new();
    [ObservableProperty] private MainContentViewMode _selectedMainContentViewMode = MainContentViewMode.Daily;
    [ObservableProperty] private ExpenseTagVM? _selectedOtherTag;
    [ObservableProperty] private ExpenseTagVM? _selectedTag;
    [ObservableProperty] private ExpenseTagVM? _selectedVisibleTag;

    [ObservableProperty] private ObservableCollection<SpendingSourceVM> _spendingSources = [];

    [ObservableProperty] private ObservableCollection<ExpenseTagVM> _tags = [];

    private decimal _totalIncomeAmount;

    [ObservableProperty] private decimal _totalSpent;

    [ObservableProperty]
    private ICollectionView _wants = CollectionViewSource.GetDefaultView(Array.Empty<ExpenseLogVM>());

    [ObservableProperty] private decimal _wantsAvailable;
    [ObservableProperty] private int _wantsPercentage;
    [ObservableProperty] private decimal _wantsSpent;
    private decimal _wantsThreshold = 0.3m;

    public MainVM(
        IViewModelReadUnitOfWork<ExpenseVM, ExpenseLogVM, IncomeLogVM, ExpenseTagVM, SavingGoalVM, SpendingSourceVM>
            readUnitOfWork,
        IUserSettingsRepository userSettingsRepository)
    {
        _readUnitOfWork = readUnitOfWork;
        _userSettingsRepository = userSettingsRepository;

        Notifications.CollectionChanged += OnNotificationsCollectionChanged;
        SavingGoals.CollectionChanged += OnSavingGoalsCollectionChanged;

        AttachExpenseLogCollectionHandlers(_needsSource);
        AttachExpenseLogCollectionHandlers(_wantsSource);
        AttachExpenseLogCollectionHandlers(_investSource);
    }

    public ObservableCollection<NotificationItemVM> Notifications { get; } = [];

    public ObservableCollection<SavingGoalVM> SavingGoals { get; } = [];

    public bool IsDailyViewSelected => SelectedMainContentViewMode == MainContentViewMode.Daily;

    public bool IsWeeklyViewSelected => SelectedMainContentViewMode == MainContentViewMode.Weekly;

    public bool IsMonthlyViewSelected => SelectedMainContentViewMode == MainContentViewMode.Monthly;

    public bool HasOtherTags => OtherTags.Count > 0;

    public bool IsSelectedTagInOtherTags => SelectedOtherTag is not null;

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

    async partial void OnSelectedDayChanged(DayOfWeekVM value)
    {
        if (value is null)
            return;

        foreach (var item in DaysOfWeek)
            item.IsSelected = ReferenceEquals(item, value);

        if (SelectedMainContentViewMode == MainContentViewMode.Daily)
            _lastSelectedDate = value.Date;

        if (!_isInitialized)
            return;

        await ReloadDataForSelectedItem(value);
    }

    partial void OnSelectedMainContentViewModeChanged(MainContentViewMode value)
    {
        OnPropertyChanged(nameof(IsDailyViewSelected));
        OnPropertyChanged(nameof(IsWeeklyViewSelected));
        OnPropertyChanged(nameof(IsMonthlyViewSelected));

        if (_isInitialized)
            NavigateSpinnerToDate(SelectedDay.Date);
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
    private void SetSelectedMainContentView(MainContentViewMode viewMode)
    {
        SelectedMainContentViewMode = viewMode;
    }

    [RelayCommand]
    private void NavigateSpinnerBack()
    {
        _spinnerPageOffset--;
        CanNavigateForward = true;
        BuildSpinnerItems();
        SelectFirstSpinnerItem();
    }

    [RelayCommand]
    private void NavigateSpinnerForward()
    {
        if (_spinnerPageOffset >= 0)
            return;

        _spinnerPageOffset++;
        CanNavigateForward = _spinnerPageOffset < 0;
        BuildSpinnerItems();
        SelectFirstSpinnerItem();
    }

    [RelayCommand]
    private void ToggleNotificationPanel()
    {
        IsNotificationPanelOpen = !IsNotificationPanelOpen;
    }

    [RelayCommand]
    private void ClearNotifications()
    {
        Notifications.Clear();
    }

    [RelayCommand]
    private void DismissNotification(NotificationItemVM? notification)
    {
        if (notification is null)
            return;

        Notifications.Remove(notification);
    }

    [RelayCommand]
    private void DeleteExpenseLog(ExpenseLogVM? expenseLog)
    {
        if (expenseLog is null)
            return;

        RemoveExpenseLogFromCollection(_needsSource, expenseLog);
        RemoveExpenseLogFromCollection(_wantsSource, expenseLog);
        RemoveExpenseLogFromCollection(_investSource, expenseLog);
    }

    internal IReadOnlyList<ExpenseLogVM> GetAllExpenseLogs()
    {
        return _needsSource.Concat(_wantsSource).Concat(_investSource).ToList();
    }

    internal IReadOnlyCollection<int> GetExpenseLogIdsMarkedForDeletion()
    {
        return _expenseLogIdsMarkedForDeletion.ToArray();
    }

    public async Task Initialize()
    {
        NavigateSpinnerToDate(DateTime.Today);
        await LoadUserSettingsAsync();

        var spendingSources = await _readUnitOfWork.SpendingSources.GetAllAsync();
        var expenses = await _readUnitOfWork.Expenses.GetByDayAsync(DateTime.Today);
        var expenseLogs = await _readUnitOfWork.ExpenseLogs.GetByDayAsync(DateTime.Today);
        var incomeLogs = await _readUnitOfWork.IncomeLogs.GetByDayAsync(DateTime.Today);
        var savingGoals = await _readUnitOfWork.SavingGoals.GetAllAsync();
        var allTags = (await _readUnitOfWork.ExpenseTags.GetTagsByCountDescendingAsync()).Select(tag => tag.Tag)
            .ToList();

        LoadExpenses(expenses);
        LoadSpendingSources(spendingSources, incomeLogs, expenseLogs);
        LoadExpenseLogs(expenseLogs);
        LoadSavingGoals(savingGoals);
        LoadTags(allTags);
        ConfigureExpenseViews();
        RefreshDashboardMetrics();

        _isInitialized = true;
        RefreshExpenseViews();
        RefreshNotifications();
    }

    private async Task ReloadDataForSelectedItem(DayOfWeekVM selectedItem)
    {
        IReadOnlyList<ExpenseVM> expenses;
        IReadOnlyList<ExpenseLogVM> expenseLogs;
        IReadOnlyList<IncomeLogVM> incomeLogs;

        switch (SelectedMainContentViewMode)
        {
            case MainContentViewMode.Daily:
                var day = selectedItem.Date;
                expenses = await _readUnitOfWork.Expenses.GetByDayAsync(day);
                expenseLogs = await _readUnitOfWork.ExpenseLogs.GetByDayAsync(day);
                incomeLogs = await _readUnitOfWork.IncomeLogs.GetByDayAsync(day);
                break;

            case MainContentViewMode.Weekly:
                var startOfWeek = selectedItem.Date;
                var endOfWeek = startOfWeek.AddDays(6);
                expenses = await _readUnitOfWork.Expenses.GetByWeekAsync(startOfWeek, endOfWeek);
                expenseLogs = await _readUnitOfWork.ExpenseLogs.GetByWeekAsync(startOfWeek, endOfWeek);
                incomeLogs = await _readUnitOfWork.IncomeLogs.GetByWeekAsync(startOfWeek, endOfWeek);
                break;

            case MainContentViewMode.Monthly:
                var month = selectedItem.Date.Month;
                expenses = await _readUnitOfWork.Expenses.GetByMonthAsync(month);
                expenseLogs = await _readUnitOfWork.ExpenseLogs.GetByMonthAsync(month);
                incomeLogs = await _readUnitOfWork.IncomeLogs.GetByMonthAsync(month);
                break;

            default:
                return;
        }

        LoadExpenses(expenses);
        LoadSpendingSources(SpendingSources, incomeLogs, expenseLogs);
        LoadExpenseLogs(expenseLogs);
        RefreshDashboardMetrics();
        RefreshNotifications();
    }

    private void NavigateSpinnerToDate(DateTime referenceDate)
    {
        ComputeSpinnerOffset(referenceDate);
        CanNavigateForward = _spinnerPageOffset < 0;
        BuildSpinnerItems();
        SelectSpinnerItemForDate(referenceDate);
    }

    private void ComputeSpinnerOffset(DateTime referenceDate)
    {
        var today = DateTime.Today;

        switch (SelectedMainContentViewMode)
        {
            case MainContentViewMode.Daily:
                var todayMonday = today.AddDays(-(int)today.DayOfWeek + 1);
                var refMonday = referenceDate.AddDays(-(int)referenceDate.DayOfWeek + 1);
                _spinnerPageOffset = (int)(refMonday - todayMonday).Days / 7;
                break;

            case MainContentViewMode.Weekly:
                var todayWeekMonday = today.AddDays(-(int)today.DayOfWeek + 1);
                var todayWeekNum = ISOWeek.GetWeekOfYear(today);
                var todayGroupStart = ((todayWeekNum - 1) / 4) * 4;
                var todayBaseMonday = todayWeekMonday.AddDays(-(todayWeekNum - 1 - todayGroupStart) * 7);

                var refWeekMonday = referenceDate.AddDays(-(int)referenceDate.DayOfWeek + 1);
                var refWeekNum = ISOWeek.GetWeekOfYear(refWeekMonday);
                var refGroupStart = ((refWeekNum - 1) / 4) * 4;
                var refBaseMonday = refWeekMonday.AddDays(-(refWeekNum - 1 - refGroupStart) * 7);

                _spinnerPageOffset = (int)(refBaseMonday - todayBaseMonday).Days / 28;
                break;

            case MainContentViewMode.Monthly:
                var todayGroupMonth = ((today.Month - 1) / 4) * 4 + 1;
                var refGroupMonth = ((referenceDate.Month - 1) / 4) * 4 + 1;
                var todayBase = new DateTime(today.Year, todayGroupMonth, 1);
                var refBase = new DateTime(referenceDate.Year, refGroupMonth, 1);
                _spinnerPageOffset = ((refBase.Year - todayBase.Year) * 12 + refBase.Month - todayBase.Month) / 4;
                break;
        }
    }

    private void BuildSpinnerItems()
    {
        switch (SelectedMainContentViewMode)
        {
            case MainContentViewMode.Daily:
                BuildDailySpinnerItems();
                break;
            case MainContentViewMode.Weekly:
                BuildWeeklySpinnerItems();
                break;
            case MainContentViewMode.Monthly:
                BuildMonthlySpinnerItems();
                break;
        }
    }

    private void BuildDailySpinnerItems()
    {
        var currentWeekMonday = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek + 1);
        var firstDay = currentWeekMonday.AddDays(_spinnerPageOffset * 7);

        DaysOfWeek = new ObservableCollection<DayOfWeekVM>(
            Enumerable.Range(0, 7).Select(offset =>
            {
                var day = firstDay.AddDays(offset);
                return new DayOfWeekVM
                {
                    Date = day,
                    DayName = day.ToString("ddd"),
                    DayNumber = day.Day.ToString(),
                    IsSelected = false
                };
            }));
    }

    private void BuildWeeklySpinnerItems()
    {
        var today = DateTime.Today;
        var currentWeekMonday = today.AddDays(-(int)today.DayOfWeek + 1);
        var currentWeekNumber = ISOWeek.GetWeekOfYear(today);
        var groupStart = ((currentWeekNumber - 1) / 4) * 4;
        var baseMonday = currentWeekMonday.AddDays(-(currentWeekNumber - 1 - groupStart) * 7);
        var firstMonday = baseMonday.AddDays(_spinnerPageOffset * 28);

        DaysOfWeek = new ObservableCollection<DayOfWeekVM>(
            Enumerable.Range(0, 4).Select(offset =>
            {
                var weekMonday = firstMonday.AddDays(offset * 7);
                var weekNumber = ISOWeek.GetWeekOfYear(weekMonday);
                return new DayOfWeekVM
                {
                    Date = weekMonday,
                    DayName = "Week",
                    DayNumber = weekNumber.ToString(),
                    IsSelected = false
                };
            }));
    }

    private void BuildMonthlySpinnerItems()
    {
        var today = DateTime.Today;
        var currentMonth = today.Month;
        var groupStartMonth = ((currentMonth - 1) / 4) * 4 + 1;
        var baseDate = new DateTime(today.Year, groupStartMonth, 1);
        var firstMonth = baseDate.AddMonths(_spinnerPageOffset * 4);

        DaysOfWeek = new ObservableCollection<DayOfWeekVM>(
            Enumerable.Range(0, 4).Select(offset =>
            {
                var monthDate = firstMonth.AddMonths(offset);
                return new DayOfWeekVM
                {
                    Date = monthDate,
                    DayName = "",
                    DayNumber = monthDate.ToString("MMM"),
                    IsSelected = false
                };
            }));
    }

    private void SelectSpinnerItemForDate(DateTime referenceDate)
    {
        DayOfWeekVM? match = SelectedMainContentViewMode switch
        {
            MainContentViewMode.Daily =>
                DaysOfWeek.FirstOrDefault(d => d.Date.Date == _lastSelectedDate.Date)
                ?? DaysOfWeek.FirstOrDefault(d => d.Date.Date == referenceDate.Date),

            MainContentViewMode.Weekly =>
                DaysOfWeek.FirstOrDefault(d =>
                    referenceDate.Date >= d.Date.Date && referenceDate.Date < d.Date.AddDays(7).Date),

            MainContentViewMode.Monthly =>
                DaysOfWeek.FirstOrDefault(d =>
                    d.Date.Year == referenceDate.Year && d.Date.Month == referenceDate.Month),

            _ => null
        };

        SelectedDay = match ?? DaysOfWeek.FirstOrDefault() ?? new DayOfWeekVM();
    }

    private void SelectFirstSpinnerItem()
    {
        SelectedDay = DaysOfWeek.FirstOrDefault() ?? new DayOfWeekVM();
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

        SpendingSources = new ObservableCollection<SpendingSourceVM>(spendingSources.Select(source =>
        {
            source.MoneyIn = moneyInBySourceId.GetValueOrDefault(source.Id);
            source.MoneyOut = moneyOutBySourceId.GetValueOrDefault(source.Id);
            return source;
        }));
    }

    private void LoadExpenseLogs(IEnumerable<ExpenseLogVM> expenseLogs)
    {
        var activeExpenseLogs = expenseLogs
            .Where(log => !log.IsForDeletion)
            .ToList();

        ReplaceExpenseLogs(_needsSource,
            activeExpenseLogs.Where(log => log.Expense?.ExpenseCategory == ExpenseCategory.Needs));
        ReplaceExpenseLogs(_wantsSource,
            activeExpenseLogs.Where(log => log.Expense?.ExpenseCategory == ExpenseCategory.Wants));
        ReplaceExpenseLogs(_investSource,
            activeExpenseLogs.Where(log => log.Expense?.ExpenseCategory == ExpenseCategory.Savings));
    }

    private void LoadSavingGoals(IEnumerable<SavingGoalVM> savingGoals)
    {
        SavingGoals.Clear();

        foreach (var goal in savingGoals) SavingGoals.Add(goal);

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
        _totalIncomeAmount = SpendingSources.Sum(source => source.Balance);

        NeedsAvailable = decimal.Round(_totalIncomeAmount * _needsThreshold, 2);
        WantsAvailable = decimal.Round(_totalIncomeAmount * _wantsThreshold, 2);
        InvestAvailable = decimal.Round(_totalIncomeAmount * _investThreshold, 2);

        NeedsSpent = _needsSource.Sum(log => log.Amount);
        WantsSpent = _wantsSource.Sum(log => log.Amount);
        InvestSpent = _investSource.Sum(log => log.Amount);
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
        return (int)((_totalIncomeAmount - TotalSpent) / daysLeft);
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
        foreach (var source in SpendingSources.Where(source =>
                     source.SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL &&
                     source.DueDate.HasValue))
        {
            var dueDate = source.DueDate!.Value.Date;
            if ((dueDate - DateTime.Today).Days != _deadlineReminderDays)
                continue;

            yield return CreateNotification(
                $"credit-deadline-{source.Id}-{dueDate:yyyyMMdd}",
                $"{source.Name} payment is coming up",
                $"{source.Name} is due on {dueDate:MMM d}.",
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
            var recurringDate = expense.RecurringDate!.Value.Date;
            if ((recurringDate - DateTime.Today).Days != _deadlineReminderDays)
                continue;

            yield return CreateNotification(
                $"recurring-deadline-{expense.Id}-{recurringDate:yyyyMMdd}",
                $"{expense.Name} deducts in {_deadlineReminderDays} days",
                $"{expense.Name} is scheduled for {recurringDate:MMM d}.",
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
                expense.RecurringDate?.Date == DateTime.Today)
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
        if (!_isLowCreditNotifEnabled)
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
        _isLowCreditNotifEnabled = ParseBool(settingsByName, UserSettingNames.IsLowCreditNotifEnabled, false);
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
        {
            DetachExpenseLogHandlers(removedExpenseLogs);

            if (_isInitialized) MarkExpenseLogsForDeletion(removedExpenseLogs);
        }

        if (addedExpenseLogs.Count > 0)
        {
            AttachExpenseLogHandlers(addedExpenseLogs);

            if (_isInitialized) RestoreExpenseLogsFromDeletion(addedExpenseLogs);
        }

        if (_isInitialized)
        {
            RefreshDashboardMetrics();
            RefreshNotifications();
        }
    }

    private void OnExpenseLogPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_isInitialized)
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

                if (_isInitialized)
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

        if (_isInitialized)
        {
            RefreshDashboardMetrics();
            RefreshNotifications();
        }
    }

    private void OnSpendingSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_isInitialized)
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

    private static void ReplaceExpenseLogs(ObservableCollection<ExpenseLogVM> target, IEnumerable<ExpenseLogVM> items)
    {
        target.Clear();

        foreach (var item in items.OrderByDescending(log => log.DeductedOn)) target.Add(item);
    }
}