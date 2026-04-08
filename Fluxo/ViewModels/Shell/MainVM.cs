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

namespace Fluxo.ViewModels.Shell
{
    public partial class MainVM : ObservableRecipient
    {
        private readonly IViewModelReadUnitOfWork<ExpenseVM, ExpenseLogVM, IncomeLogVM, ExpenseTagVM, SavingGoalVM, SpendingSourceVM> _readUnitOfWork;
        private readonly IUserSettingsRepository _userSettingsRepository;

        private readonly ObservableCollection<ExpenseLogVM> _needsSource = [];
        private readonly ObservableCollection<ExpenseLogVM> _wantsSource = [];
        private readonly ObservableCollection<ExpenseLogVM> _investSource = [];
        private readonly ObservableCollection<SavingGoalVM> _savingGoals = [];
        private readonly ObservableCollection<NotificationItemVM> _notifications = [];
        private readonly HashSet<int> _expenseLogIdsMarkedForDeletion = [];
        private readonly List<ExpenseVM> _expenses = [];

        private decimal _totalIncomeAmount;
        private bool _isInitialized;
        private decimal _needsThreshold = 0.5m;
        private decimal _wantsThreshold = 0.3m;
        private decimal _investThreshold = 0.2m;
        private int _deadlineReminderDays = 7;
        private decimal _budgetUsageWarningPercentage = 0.90m;
        private decimal _creditUsageWarningPercentage = 0.30m;
        private decimal _lowAccountBalancePercentage = 0.20m;
        private bool _isFixedExpensesDeductionNotifEnabled;
        private bool _isLowCreditNotifEnabled;

        [ObservableProperty] private ObservableCollection<SpendingSourceVM> _spendingSources = [];

        [ObservableProperty] private ObservableCollection<ExpenseTagVM> _tags = [];
        [ObservableProperty] private ObservableCollection<ExpenseTagVM> _otherTags = [];
        [ObservableProperty] private ExpenseTagVM? _selectedTag;

        [ObservableProperty] private ObservableCollection<DayOfWeekVM> _daysOfWeek = [];
        [ObservableProperty] private DayOfWeekVM _selectedDay = new();

        [ObservableProperty] private bool _isNeedsEmpty;
        [ObservableProperty] private bool _isWantsEmpty;
        [ObservableProperty] private bool _isInvestEmpty;

        [ObservableProperty] private decimal _needsSpent;
        [ObservableProperty] private decimal _wantsSpent;
        [ObservableProperty] private decimal _investSpent;

        [ObservableProperty] private decimal _needsAvailable;
        [ObservableProperty] private decimal _wantsAvailable;
        [ObservableProperty] private decimal _investAvailable;

        [ObservableProperty] private int _needsPercentage;
        [ObservableProperty] private int _wantsPercentage;
        [ObservableProperty] private int _investPercentage;

        [ObservableProperty] private int _dailyAllowance;

        [ObservableProperty] private decimal _totalSpent;

        [ObservableProperty] private bool _hasNotifications;
        [ObservableProperty] private int _notificationCount;
        [ObservableProperty] private bool _isNotificationPanelOpen;

        [ObservableProperty] private ICollectionView _needs = CollectionViewSource.GetDefaultView(Array.Empty<ExpenseLogVM>());
        [ObservableProperty] private ICollectionView _wants = CollectionViewSource.GetDefaultView(Array.Empty<ExpenseLogVM>());
        [ObservableProperty] private ICollectionView _invest = CollectionViewSource.GetDefaultView(Array.Empty<ExpenseLogVM>());

        public MainVM(
            IViewModelReadUnitOfWork<ExpenseVM, ExpenseLogVM, IncomeLogVM, ExpenseTagVM, SavingGoalVM, SpendingSourceVM> readUnitOfWork,
            IUserSettingsRepository userSettingsRepository)
        {
            _readUnitOfWork = readUnitOfWork;
            _userSettingsRepository = userSettingsRepository;

            _notifications.CollectionChanged += OnNotificationsCollectionChanged;
            _savingGoals.CollectionChanged += OnSavingGoalsCollectionChanged;

            AttachExpenseLogCollectionHandlers(_needsSource);
            AttachExpenseLogCollectionHandlers(_wantsSource);
            AttachExpenseLogCollectionHandlers(_investSource);
        }

        public ObservableCollection<NotificationItemVM> Notifications => _notifications;

        partial void OnSelectedTagChanged(ExpenseTagVM? value)
        {
            RefreshExpenseViews();
        }

        partial void OnSpendingSourcesChanged(ObservableCollection<SpendingSourceVM>? oldValue, ObservableCollection<SpendingSourceVM> newValue)
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
        private void ToggleNotificationPanel()
        {
            IsNotificationPanelOpen = !IsNotificationPanelOpen;
        }

        [RelayCommand]
        private void ClearNotifications()
        {
            _notifications.Clear();
        }

        [RelayCommand]
        private void DismissNotification(NotificationItemVM? notification)
        {
            if (notification is null)
                return;

            _notifications.Remove(notification);
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

        internal IReadOnlyCollection<int> GetExpenseLogIdsMarkedForDeletion()
        {
            return _expenseLogIdsMarkedForDeletion.ToArray();
        }

        public async Task Initialize()
        {
            BuildDaysOfWeek();
            await LoadUserSettingsAsync();

            var spendingSources = await _readUnitOfWork.SpendingSources.GetAllAsync();
            var expenses = await _readUnitOfWork.Expenses.GetAllAsync();
            var expenseLogs = await _readUnitOfWork.ExpenseLogs.GetAllAsync();
            var incomeLogs = await _readUnitOfWork.IncomeLogs.GetAllAsync();
            var savingGoals = await _readUnitOfWork.SavingGoals.GetAllAsync();
            var allTags = (await _readUnitOfWork.ExpenseTags.GetTagsByCountDescendingAsync()).Select(tag => tag.Tag).ToList();

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

        private void BuildDaysOfWeek()
        {
            var firstDayOfWeek = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek + 1);
            var daysThisWeek = Enumerable.Range(0, 7)
                .Select(offset => firstDayOfWeek.AddDays(offset))
                .ToList();

            DaysOfWeek = new ObservableCollection<DayOfWeekVM>(daysThisWeek.Select(day => new DayOfWeekVM
            {
                Date = day,
                DayName = day.ToString("ddd"),
                DayNumber = day.Day.ToString(),
                IsSelected = day.Date == DateTime.Today
            }));

            SelectedDay = DaysOfWeek.FirstOrDefault(day => day.IsSelected) ?? DaysOfWeek.FirstOrDefault() ?? new DayOfWeekVM();
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

            ReplaceExpenseLogs(_needsSource, activeExpenseLogs.Where(log => log.Expense?.ExpenseCategory == ExpenseCategory.Needs));
            ReplaceExpenseLogs(_wantsSource, activeExpenseLogs.Where(log => log.Expense?.ExpenseCategory == ExpenseCategory.Wants));
            ReplaceExpenseLogs(_investSource, activeExpenseLogs.Where(log => log.Expense?.ExpenseCategory == ExpenseCategory.Savings));
        }

        private void LoadSavingGoals(IEnumerable<SavingGoalVM> savingGoals)
        {
            _savingGoals.Clear();

            foreach (var goal in savingGoals)
            {
                _savingGoals.Add(goal);
            }
        }

        private void LoadTags(IEnumerable<ExpenseTagVM> allTags)
        {
            Tags = new ObservableCollection<ExpenseTagVM>(allTags.Take(5));
            OtherTags = new ObservableCollection<ExpenseTagVM>(allTags.Skip(5));
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
            var daysLeft = Math.Max(1, DateTime.DaysInMonth(DateTime.Today.Year, DateTime.Today.Month) - DateTime.Today.Day);
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
                    key: $"credit-deadline-{source.Id}-{dueDate:yyyyMMdd}",
                    title: $"{source.Name} payment is coming up",
                    message: $"{source.Name} is due on {dueDate:MMM d}.",
                    severity: NotificationSeverity.Warning,
                    isSystemGenerated: true);
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
                    key: $"recurring-deadline-{expense.Id}-{recurringDate:yyyyMMdd}",
                    title: $"{expense.Name} deducts in {_deadlineReminderDays} days",
                    message: $"{expense.Name} is scheduled for {recurringDate:MMM d}.",
                    severity: NotificationSeverity.Warning,
                    isSystemGenerated: true);
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
                key: $"auto-expenses-{DateTime.Today:yyyyMMdd}",
                title: "Auto-expenses processed today",
                message: $"{expenseSummary} reached {verb} scheduled date today.",
                severity: NotificationSeverity.Success,
                isSystemGenerated: true);
        }

        private IEnumerable<NotificationItemVM> GetBudgetThresholdNotifications()
        {
            if (HasCrossedBudgetThreshold(NeedsSpent, NeedsAvailable))
            {
                yield return CreateNotification(
                    key: "budget-threshold-needs",
                    title: "Needs budget is almost fully spent",
                    message: $"Needs has reached {NeedsPercentage}% of its allocation.",
                    severity: NotificationSeverity.Danger,
                    isSystemGenerated: true);
            }

            if (HasCrossedBudgetThreshold(WantsSpent, WantsAvailable))
            {
                yield return CreateNotification(
                    key: "budget-threshold-wants",
                    title: "Wants budget is almost fully spent",
                    message: $"Wants has reached {WantsPercentage}% of its allocation.",
                    severity: NotificationSeverity.Warning,
                    isSystemGenerated: true);
            }

            if (HasCrossedBudgetThreshold(InvestSpent, InvestAvailable))
            {
                yield return CreateNotification(
                    key: "budget-threshold-savings",
                    title: "Savings budget is almost fully spent",
                    message: $"Savings has reached {InvestPercentage}% of its allocation.",
                    severity: NotificationSeverity.Warning,
                    isSystemGenerated: true);
            }
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
                    key: $"credit-usage-{source.Id}",
                    title: $"{source.Name} crossed the credit threshold",
                    message: $"{source.Name} is using {usagePercentage}% of its limit.",
                    severity: NotificationSeverity.Warning,
                    isSystemGenerated: true);
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
                    key: $"low-account-{source.Id}",
                    title: $"{source.Name} is running low",
                    message: $"{source.Name} is down to {balancePercentage}% of its pre-spend total.",
                    severity: NotificationSeverity.Danger,
                    isSystemGenerated: true);
            }
        }

        private async Task LoadUserSettingsAsync()
        {
            var settings = await _userSettingsRepository.GetAllAsync();
            var settingsByName = settings.ToDictionary(setting => setting.Name, setting => setting.Value, StringComparer.Ordinal);

            _needsThreshold = ParsePercentage(settingsByName, UserSettingNames.NeedsThreshold, 50m);
            _wantsThreshold = ParsePercentage(settingsByName, UserSettingNames.WantsThreshold, 30m);
            _investThreshold = ParsePercentage(settingsByName, UserSettingNames.InvestThreshold, 20m);
            _deadlineReminderDays = ParseInt(settingsByName, UserSettingNames.DeadlineReminderDays, 7);
            _budgetUsageWarningPercentage = ParseDecimal(settingsByName, UserSettingNames.BudgetUsageWarningPercentage, 0.90m);
            _creditUsageWarningPercentage = ParseDecimal(settingsByName, UserSettingNames.CreditUsageWarningPercentage, 0.30m);
            _lowAccountBalancePercentage = ParseDecimal(settingsByName, UserSettingNames.LowAccountBalancePercentage, 0.20m);
            _isFixedExpensesDeductionNotifEnabled = ParseBool(settingsByName, UserSettingNames.IsFixedExpensesDeductionNotifEnabled, false);
            _isLowCreditNotifEnabled = ParseBool(settingsByName, UserSettingNames.IsLowCreditNotifEnabled, false);
        }

        private bool HasCrossedBudgetThreshold(decimal spentAmount, decimal availableAmount)
        {
            if (availableAmount <= 0)
                return false;

            return spentAmount / availableAmount >= _budgetUsageWarningPercentage;
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
            {
                return parsedValue;
            }

            return defaultValue;
        }

        private static int ParseInt(IReadOnlyDictionary<string, string> settings, string name, int defaultValue)
        {
            if (settings.TryGetValue(name, out var value) && int.TryParse(value, out var parsedValue))
            {
                return parsedValue;
            }

            return defaultValue;
        }

        private static bool ParseBool(IReadOnlyDictionary<string, string> settings, string name, bool defaultValue)
        {
            if (settings.TryGetValue(name, out var value) && bool.TryParse(value, out var parsedValue))
            {
                return parsedValue;
            }

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
            var incomingNotificationsByKey = notifications.ToDictionary(notification => notification.Key, StringComparer.Ordinal);

            var staleNotifications = _notifications
                .Where(notification => notification.IsSystemGenerated && !incomingNotificationsByKey.ContainsKey(notification.Key))
                .ToList();

            foreach (var notification in staleNotifications)
            {
                _notifications.Remove(notification);
            }

            foreach (var incomingNotification in incomingNotificationsByKey.Values)
            {
                var existingNotification = _notifications.FirstOrDefault(notification =>
                    string.Equals(notification.Key, incomingNotification.Key, StringComparison.Ordinal));

                if (existingNotification is null)
                {
                    _notifications.Insert(0, incomingNotification);
                    continue;
                }

                UpdateNotification(existingNotification, incomingNotification);
            }

            SortNotifications();
        }

        private void UpsertEventNotification(NotificationItemVM notification)
        {
            var existingNotification = _notifications.FirstOrDefault(existing =>
                string.Equals(existing.Key, notification.Key, StringComparison.Ordinal));

            if (existingNotification is null)
            {
                _notifications.Insert(0, notification);
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
            var orderedNotifications = _notifications
                .OrderByDescending(notification => notification.CreatedOn)
                .ToList();

            for (var index = 0; index < orderedNotifications.Count; index++)
            {
                var currentIndex = _notifications.IndexOf(orderedNotifications[index]);
                if (currentIndex >= 0 && currentIndex != index)
                {
                    _notifications.Move(currentIndex, index);
                }
            }
        }

        private bool FilterBySelectedTag(object item)
        {
            if (item is not ExpenseLogVM expenseLog)
            {
                return false;
            }

            if (SelectedTag is null)
            {
                return true;
            }

            return expenseLog.Expense?.ExpenseTag?.Id == SelectedTag.Id;
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
            foreach (var expenseLog in expenseLogs)
            {
                expenseLog.PropertyChanged += OnExpenseLogPropertyChanged;
            }
        }

        private void DetachExpenseLogHandlers(IEnumerable<ExpenseLogVM> expenseLogs)
        {
            foreach (var expenseLog in expenseLogs)
            {
                expenseLog.PropertyChanged -= OnExpenseLogPropertyChanged;
            }
        }

        private void OnExpenseLogCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            var removedExpenseLogs = e.OldItems?.OfType<ExpenseLogVM>().ToList() ?? [];
            var addedExpenseLogs = e.NewItems?.OfType<ExpenseLogVM>().ToList() ?? [];

            if (removedExpenseLogs.Count > 0)
            {
                DetachExpenseLogHandlers(removedExpenseLogs);

                if (_isInitialized)
                {
                    MarkExpenseLogsForDeletion(removedExpenseLogs);
                }
            }

            if (addedExpenseLogs.Count > 0)
            {
                AttachExpenseLogHandlers(addedExpenseLogs);

                if (_isInitialized)
                {
                    RestoreExpenseLogsFromDeletion(addedExpenseLogs);
                }
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
            {
                foreach (var goal in e.OldItems.OfType<SavingGoalVM>())
                {
                    goal.PropertyChanged -= OnSavingGoalPropertyChanged;
                }
            }

            if (e.NewItems is not null)
            {
                foreach (var goal in e.NewItems.OfType<SavingGoalVM>())
                {
                    goal.PropertyChanged += OnSavingGoalPropertyChanged;

                    if (_isInitialized)
                    {
                        UpsertEventNotification(CreateNotification(
                            key: goal.Id > 0 ? $"saving-goal-created-{goal.Id}" : $"saving-goal-created-{Guid.NewGuid():N}",
                            title: "New saving goal created",
                            message: $"{goal.Name} was added to your savings goals.",
                            severity: NotificationSeverity.Success,
                            isSystemGenerated: false));
                    }
                }
            }
        }

        private void OnSavingGoalPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!_isInitialized || sender is not SavingGoalVM goal || goal.Id <= 0)
                return;

            var notification = _notifications.FirstOrDefault(item =>
                string.Equals(item.Key, $"saving-goal-created-{goal.Id}", StringComparison.Ordinal));

            if (notification is null)
                return;

            notification.Message = $"{goal.Name} was added to your savings goals.";
        }

        private void OnNotificationsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            NotificationCount = _notifications.Count;
            HasNotifications = NotificationCount > 0;
        }

        private void AttachSpendingSourceHandlers(IEnumerable<SpendingSourceVM> spendingSources)
        {
            foreach (var spendingSource in spendingSources)
            {
                spendingSource.PropertyChanged += OnSpendingSourcePropertyChanged;
            }
        }

        private void DetachSpendingSourceHandlers(IEnumerable<SpendingSourceVM> spendingSources)
        {
            foreach (var spendingSource in spendingSources)
            {
                spendingSource.PropertyChanged -= OnSpendingSourcePropertyChanged;
            }
        }

        private void OnSpendingSourcesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems is not null)
            {
                DetachSpendingSourceHandlers(e.OldItems.OfType<SpendingSourceVM>());
            }

            if (e.NewItems is not null)
            {
                AttachSpendingSourceHandlers(e.NewItems.OfType<SpendingSourceVM>());
            }

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

                if (expenseLog.Id > 0)
                {
                    _expenseLogIdsMarkedForDeletion.Add(expenseLog.Id);
                }
            }
        }

        private void RestoreExpenseLogsFromDeletion(IEnumerable<ExpenseLogVM> expenseLogs)
        {
            foreach (var expenseLog in expenseLogs.Where(log => log.IsForDeletion))
            {
                expenseLog.IsForDeletion = false;

                if (expenseLog.Id > 0)
                {
                    _expenseLogIdsMarkedForDeletion.Remove(expenseLog.Id);
                }
            }
        }

        private static void RemoveExpenseLogFromCollection(ObservableCollection<ExpenseLogVM> expenseLogs, ExpenseLogVM expenseLog)
        {
            var existingExpenseLog = expenseLogs.FirstOrDefault(log =>
                ReferenceEquals(log, expenseLog) || (expenseLog.Id > 0 && log.Id == expenseLog.Id));

            if (existingExpenseLog is not null)
            {
                expenseLogs.Remove(existingExpenseLog);
            }
        }

        private static void ReplaceExpenseLogs(ObservableCollection<ExpenseLogVM> target, IEnumerable<ExpenseLogVM> items)
        {
            target.Clear();

            foreach (var item in items.OrderByDescending(log => log.DeductedOn))
            {
                target.Add(item);
            }
        }
    }
}
