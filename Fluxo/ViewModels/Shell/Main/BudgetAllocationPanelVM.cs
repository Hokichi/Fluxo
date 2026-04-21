using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using AutoMapper;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Constants;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Resources.Messages;
using Fluxo.Services.History;
using Fluxo.ViewModels.Entities;

namespace Fluxo.ViewModels.Shell.Main;

public partial class BudgetAllocationPanelVM : ObservableRecipient,
    IRecipient<DateRangeSelectionChangedMessage>,
    IRecipient<AllTimeViewModeMessage>,
    IRecipient<DashboardDataInvalidatedMessage>,
    IRecipient<RecordLogMemoryMessage>,
    IRecipient<LogMemoryActionAppliedMessage>
{
    private readonly IDataOperationRunner _dataOperationRunner;
    private readonly IExpenseLogService _expenseLogService;
    private readonly IMapper _mapper;
    private readonly ObservableCollection<ExpenseLogVM> _investSource = [];
    private readonly ObservableCollection<ExpenseLogVM> _needsSource = [];
    private readonly SemaphoreSlim _reloadGate = new(1, 1);
    private readonly ISpendingSourceService _spendingSourceService;
    private readonly ITagService _tagService;
    private readonly ObservableCollection<ExpenseLogVM> _wantsSource = [];
    private readonly ObservableCollection<SpendingSourceVM> _spendingSources = [];

    private List<ExpenseLogVM> _allExpenseLogs = [];
    private List<IncomeLogVM> _allIncomeLogs = [];
    private bool _isSynchronizingTagSelections;
    private decimal _needsThreshold = 0.5m;
    private decimal _wantsThreshold = 0.3m;
    private decimal _investThreshold = 0.2m;
    private (DateTime From, DateTime To)? _selectedRange = (DateTime.Today, DateTime.Today);

    public BudgetAllocationPanelVM(
        IExpenseLogService expenseLogService,
        ISpendingSourceService spendingSourceService,
        ITagService tagService,
        IDataOperationRunner dataOperationRunner,
        IMapper mapper,
        IMessenger? messenger = null)
        : base(messenger ?? WeakReferenceMessenger.Default)
    {
        _expenseLogService = expenseLogService;
        _spendingSourceService = spendingSourceService;
        _tagService = tagService;
        _dataOperationRunner = dataOperationRunner;
        _mapper = mapper;

        Initialize();
    }

    [ObservableProperty]
    private decimal _totalSpent;

    [ObservableProperty]
    private int _dailyAllowance;

    [ObservableProperty]
    private decimal _needsAvailable;

    [ObservableProperty]
    private decimal _wantsAvailable;

    [ObservableProperty]
    private decimal _investAvailable;

    [ObservableProperty]
    private decimal _needsSpent;

    [ObservableProperty]
    private decimal _wantsSpent;

    [ObservableProperty]
    private decimal _investSpent;

    [ObservableProperty]
    private int _needsPercentage;

    [ObservableProperty]
    private int _wantsPercentage;

    [ObservableProperty]
    private int _investPercentage;

    [ObservableProperty]
    private ICollectionView _needs = CollectionViewSource.GetDefaultView(Array.Empty<ExpenseLogVM>());

    [ObservableProperty]
    private ICollectionView _wants = CollectionViewSource.GetDefaultView(Array.Empty<ExpenseLogVM>());

    [ObservableProperty]
    private ICollectionView _invest = CollectionViewSource.GetDefaultView(Array.Empty<ExpenseLogVM>());

    [ObservableProperty]
    private bool _isNeedsEmpty;

    [ObservableProperty]
    private bool _isWantsEmpty;

    [ObservableProperty]
    private bool _isInvestEmpty;

    [ObservableProperty]
    private ObservableCollection<ExpenseTagVM> _tags = [];

    [ObservableProperty]
    private ObservableCollection<ExpenseTagVM> _otherTags = [];

    [ObservableProperty]
    private ExpenseTagVM? _selectedTag;

    [ObservableProperty]
    private ExpenseTagVM? _selectedVisibleTag;

    [ObservableProperty]
    private ExpenseTagVM? _selectedOtherTag;

    [ObservableProperty]
    private int? _selectedSpendingSourceId;

    public bool HasOtherTags => OtherTags.Count > 0;

    public bool IsSelectedTagInOtherTags => SelectedOtherTag is not null;

    public ObservableCollection<SpendingSourceVM> SpendingSources => _spendingSources;

    public decimal TotalIncomeAmount => _spendingSources.Sum(source => source.Balance);

    public IReadOnlyList<ExpenseLogVM> GetAllExpenseLogs() => _allExpenseLogs.ToList();

    public void Receive(DateRangeSelectionChangedMessage message)
    {
        _selectedRange = message.Value;
        ApplyVisibleExpenseLogs();
        RefreshSourceDifferences();
    }

    public void Receive(AllTimeViewModeMessage message)
    {
        _selectedRange = null;
        ApplyVisibleExpenseLogs();
        RefreshSourceDifferences();
    }

    public void Receive(DashboardDataInvalidatedMessage message)
    {
        if (!message.Value.HasFlag(DashboardDataInvalidationScope.Budget))
            return;

        _ = ReloadFromServicesAsync();
    }

    public void Receive(RecordLogMemoryMessage message)
    {
        ApplyBalanceImpactsFromAction(message.Value, LogMemoryApplyDirection.Redo);
    }

    public void Receive(LogMemoryActionAppliedMessage message)
    {
        var (action, direction) = message.Value;
        ApplyBalanceImpactsFromAction(action, direction);
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await LoadUserSettingsAsync(cancellationToken);

        var expenseLogs = _mapper.Map<IReadOnlyList<ExpenseLogVM>>(
            await _expenseLogService.GetAllAsync(cancellationToken));
        var incomeLogs = await LoadIncomeLogsAsync(cancellationToken);
        var spendingSources = _mapper.Map<IReadOnlyList<SpendingSourceVM>>(
            await _spendingSourceService.GetAllAsync(cancellationToken));
        var tags = _mapper.Map<IReadOnlyList<ExpenseTagVM>>(
            await _tagService.GetAllAsync(cancellationToken));

        _allExpenseLogs = expenseLogs
            .Where(log => !log.IsForDeletion)
            .OrderByDescending(log => log.DeductedOn)
            .ToList();
        _allIncomeLogs = incomeLogs
            .OrderByDescending(log => log.AddedOn)
            .ToList();

        _spendingSources.Clear();
        foreach (var source in spendingSources)
            _spendingSources.Add(source);

        if (SelectedSpendingSourceId is int selectedId &&
            _spendingSources.All(source => source.Id != selectedId))
            SelectedSpendingSourceId = null;
        else
            SynchronizeSpendingSourceSelections(SelectedSpendingSourceId);

        LoadTags(tags);
        RefreshBudgetMetrics();
        ApplyVisibleExpenseLogs();
        RefreshSourceDifferences();
    }

    partial void OnSelectedTagChanged(ExpenseTagVM? value)
    {
        SynchronizeTagSelections(value);
        OnPropertyChanged(nameof(IsSelectedTagInOtherTags));
        RefreshExpenseViews();
    }

    partial void OnSelectedSpendingSourceIdChanged(int? value)
    {
        SynchronizeSpendingSourceSelections(value);
        RefreshExpenseViews();
    }

    partial void OnSelectedVisibleTagChanged(ExpenseTagVM? value)
    {
        if (_isSynchronizingTagSelections)
            return;

        SelectedTag = value;
    }

    partial void OnSelectedOtherTagChanged(ExpenseTagVM? value)
    {
        if (_isSynchronizingTagSelections)
            return;

        SelectedTag = value;
    }

    [RelayCommand]
    private void ClearSelectedTag()
    {
        SelectedTag = null;
    }

    public void ToggleSelectedSpendingSource(SpendingSourceVM? source)
    {
        var selectedId = source?.Id;
        if (selectedId is null)
            return;

        SelectedSpendingSourceId = SelectedSpendingSourceId == selectedId
            ? null
            : selectedId;
    }

    [RelayCommand]
    private async Task DeleteExpenseLog(ExpenseLogVM? expenseLog)
    {
        if (expenseLog is null || expenseLog.IsForDeletion)
            return;

        await _expenseLogService.DeleteAsync(expenseLog.Id);

        ApplyDeletedExpenseLogToUi(expenseLog);
    }

    private void Initialize()
    {
        ConfigureExpenseViews();
        IsActive = true;
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

    private async Task<IReadOnlyList<IncomeLogVM>> LoadIncomeLogsAsync(CancellationToken cancellationToken)
    {
        return await _dataOperationRunner.RunAsync(async (scope, ct) =>
        {
            var incomeLogs = await scope.UnitOfWork.IncomeLogs.GetAllAsync(ct);
            return incomeLogs
                .Select(log => new IncomeLogVM
                {
                    Id = log.Id,
                    Amount = log.Amount,
                    AddedOn = log.AddedOn,
                    Notes = log.Notes,
                    SpendingSource = new SpendingSourceVM
                    {
                        Id = log.SpendingSourceId,
                        Name = log.SpendingSource?.Name ?? string.Empty,
                        SpendingSourceType = log.SpendingSource?.SpendingSourceType ?? SpendingSourceType.Checking
                    }
                })
                .ToList();
        }, cancellationToken);
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

        Needs.Filter = FilterExpenseLog;
        Wants.Filter = FilterExpenseLog;
        Invest.Filter = FilterExpenseLog;
    }

    private void RefreshBudgetMetrics()
    {
        var totalIncomeAmount = _spendingSources.Sum(source => source.Balance);

        NeedsAvailable = decimal.Round(totalIncomeAmount * _needsThreshold, 2);
        WantsAvailable = decimal.Round(totalIncomeAmount * _wantsThreshold, 2);
        InvestAvailable = decimal.Round(totalIncomeAmount * _investThreshold, 2);

        NeedsSpent = _allExpenseLogs
            .Where(log => log.Expense?.ExpenseCategory == ExpenseCategory.Needs)
            .Sum(log => log.Amount);
        WantsSpent = _allExpenseLogs
            .Where(log => log.Expense?.ExpenseCategory == ExpenseCategory.Wants)
            .Sum(log => log.Amount);
        InvestSpent = _allExpenseLogs
            .Where(log => log.Expense?.ExpenseCategory == ExpenseCategory.Savings)
            .Sum(log => log.Amount);
        TotalSpent = NeedsSpent + WantsSpent + InvestSpent;

        NeedsPercentage = CalculatePercentage(NeedsSpent, NeedsAvailable);
        WantsPercentage = CalculatePercentage(WantsSpent, WantsAvailable);
        InvestPercentage = CalculatePercentage(InvestSpent, InvestAvailable);
        DailyAllowance = CalculateDailyAllowance(totalIncomeAmount);
    }

    private int CalculateDailyAllowance(decimal totalIncomeAmount)
    {
        var daysLeft = Math.Max(
            1,
            DateTime.DaysInMonth(DateTime.Today.Year, DateTime.Today.Month) - DateTime.Today.Day);

        return (int)((totalIncomeAmount * (1 - _investThreshold) - TotalSpent) / daysLeft);
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

    private static int CalculatePercentage(decimal spentAmount, decimal availableAmount)
    {
        if (availableAmount <= 0)
            return 0;

        return (int)Math.Round(spentAmount / availableAmount * 100, MidpointRounding.AwayFromZero);
    }

    private void ApplyVisibleExpenseLogs()
    {
        var visibleExpenseLogs = _selectedRange is { } range
            ? _allExpenseLogs.Where(log => log.DeductedOn.Date >= range.From.Date && log.DeductedOn.Date <= range.To.Date)
            : _allExpenseLogs;

        ReplaceExpenseLogs(
            _needsSource,
            visibleExpenseLogs.Where(log => log.Expense?.ExpenseCategory == ExpenseCategory.Needs));
        ReplaceExpenseLogs(
            _wantsSource,
            visibleExpenseLogs.Where(log => log.Expense?.ExpenseCategory == ExpenseCategory.Wants));
        ReplaceExpenseLogs(
            _investSource,
            visibleExpenseLogs.Where(log => log.Expense?.ExpenseCategory == ExpenseCategory.Savings));

        RefreshExpenseViews();
    }

    private bool FilterExpenseLog(object item)
    {
        if (item is not ExpenseLogVM expenseLog)
            return false;

        if (SelectedTag is not null &&
            expenseLog.Expense?.ExpenseTag?.Id != SelectedTag.Id)
            return false;

        return SelectedSpendingSourceId is null ||
               expenseLog.SpendingSource?.Id == SelectedSpendingSourceId;
    }

    private void SynchronizeTagSelections(ExpenseTagVM? selectedTag)
    {
        _isSynchronizingTagSelections = true;

        try
        {
            SelectedVisibleTag = selectedTag is null
                ? null
                : Enumerable.FirstOrDefault<ExpenseTagVM>(Tags, tag => tag.Id == selectedTag.Id);
            SelectedOtherTag = selectedTag is null
                ? null
                : Enumerable.FirstOrDefault<ExpenseTagVM>(OtherTags, tag => tag.Id == selectedTag.Id);
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

    private void SynchronizeSpendingSourceSelections(int? selectedSpendingSourceId)
    {
        foreach (var source in _spendingSources)
            source.IsSelected = selectedSpendingSourceId is int id && source.Id == id;
    }

    private void ApplyDeletedExpenseLogToUi(ExpenseLogVM expenseLog)
    {
        var trackedExpenseLog = _allExpenseLogs.FirstOrDefault(log => log.Id == expenseLog.Id) ?? expenseLog;
        var snapshot = CreateExpenseLogSnapshot(trackedExpenseLog);
        Messenger.Send(new RecordLogMemoryMessage(new DeleteExpenseLogMemoryAction(snapshot)));
    }

    private void RefreshSourceDifferences()
    {
        var visibleExpenses = EnumerateVisibleExpenseLogs();
        var visibleIncomes = EnumerateVisibleIncomeLogs();

        var expenseBySource = visibleExpenses
            .GroupBy(log => log.SpendingSource?.Id ?? 0)
            .ToDictionary(group => group.Key, group => group.Sum(log => log.Amount));
        var incomeBySource = visibleIncomes
            .GroupBy(log => log.SpendingSource?.Id ?? 0)
            .ToDictionary(group => group.Key, group => group.Sum(log => log.Amount));

        foreach (var source in _spendingSources)
        {
            var expense = expenseBySource.GetValueOrDefault(source.Id);
            var income = incomeBySource.GetValueOrDefault(source.Id);
            source.Difference = income - expense;
        }
    }

    private IEnumerable<ExpenseLogVM> EnumerateVisibleExpenseLogs()
    {
        var source = _allExpenseLogs.Where(log => !log.IsForDeletion);
        if (_selectedRange is not { } range)
            return source;

        return source.Where(log => log.DeductedOn.Date >= range.From.Date && log.DeductedOn.Date <= range.To.Date);
    }

    private IEnumerable<IncomeLogVM> EnumerateVisibleIncomeLogs()
    {
        if (_selectedRange is not { } range)
            return _allIncomeLogs;

        return _allIncomeLogs.Where(log => log.AddedOn.Date >= range.From.Date && log.AddedOn.Date <= range.To.Date);
    }

    private void ApplyBalanceImpactsFromAction(ILogMemoryAction action, LogMemoryApplyDirection direction)
    {
        switch (action)
        {
            case CompositeLogMemoryAction compositeAction:
                foreach (var childAction in compositeAction.Actions)
                    ApplyBalanceImpactsFromAction(childAction, direction);
                return;

            case AddExpenseLogMemoryAction addExpenseAction:
                ApplyExpenseAction(addExpenseAction.Snapshot, direction);
                return;

            case AddIncomeLogMemoryAction addIncomeAction:
                ApplyIncomeAction(addIncomeAction.Snapshot, direction);
                return;

            case EditExpenseLogMemoryAction editExpenseAction:
                ApplyEditedExpenseAction(editExpenseAction, direction);
                return;

            case DeleteExpenseLogMemoryAction deleteExpenseAction:
                ApplyDeletedExpenseAction(deleteExpenseAction, direction);
                return;
        }
    }

    private void ApplyExpenseAction(ExpenseLogMemorySnapshot snapshot, LogMemoryApplyDirection direction)
    {
        if (direction == LogMemoryApplyDirection.Redo)
        {
            UpsertExpenseLog(snapshot);
            AdjustSourceDifference(snapshot.SpendingSourceId, snapshot.DeductedOn, snapshot.Amount);
            RefreshExpenseBucketsFromTrackedLogs();
            return;
        }

        RemoveExpenseLog(snapshot.ExpenseLogId);
        AdjustSourceDifference(snapshot.SpendingSourceId, snapshot.DeductedOn, -snapshot.Amount);
        RefreshExpenseBucketsFromTrackedLogs();
    }

    private void ApplyIncomeAction(IncomeLogMemorySnapshot snapshot, LogMemoryApplyDirection direction)
    {
        if (direction == LogMemoryApplyDirection.Redo)
        {
            UpsertIncomeLog(snapshot);
            AdjustSourceDifference(snapshot.SpendingSourceId, snapshot.AddedOn, -snapshot.Amount);
            return;
        }

        RemoveIncomeLog(snapshot.IncomeLogId);
        AdjustSourceDifference(snapshot.SpendingSourceId, snapshot.AddedOn, snapshot.Amount);
    }

    private void ApplyEditedExpenseAction(EditExpenseLogMemoryAction action, LogMemoryApplyDirection direction)
    {
        var previous = direction == LogMemoryApplyDirection.Redo ? action.Before : action.After;
        var target = direction == LogMemoryApplyDirection.Redo ? action.After : action.Before;

        UpsertExpenseLog(target);
        AdjustSourceDifference(previous.SpendingSourceId, previous.DeductedOn, -previous.Amount);
        AdjustSourceDifference(target.SpendingSourceId, target.DeductedOn, target.Amount);
        RefreshExpenseBucketsFromTrackedLogs();
    }

    private void ApplyDeletedExpenseAction(DeleteExpenseLogMemoryAction action, LogMemoryApplyDirection direction)
    {
        if (action.Snapshot is not { } snapshot)
            return;

        if (direction == LogMemoryApplyDirection.Redo)
        {
            RemoveExpenseLog(snapshot.ExpenseLogId);
            AdjustSourceDifference(snapshot.SpendingSourceId, snapshot.DeductedOn, -snapshot.Amount);
            RefreshExpenseBucketsFromTrackedLogs();
            return;
        }

        UpsertExpenseLog(snapshot);
        AdjustSourceDifference(snapshot.SpendingSourceId, snapshot.DeductedOn, snapshot.Amount);
        RefreshExpenseBucketsFromTrackedLogs();
    }

    private void AdjustSourceDifference(int sourceId, DateTime occurredOn, decimal deltaDifference)
    {
        if (!IsDateVisible(occurredOn))
            return;

        var source = _spendingSources.FirstOrDefault(candidate => candidate.Id == sourceId);
        if (source is null)
            return;

        source.Difference += deltaDifference;
    }

    private bool IsDateVisible(DateTime date)
    {
        if (_selectedRange is not { } range)
            return true;

        var entryDate = date.Date;
        return entryDate >= range.From.Date && entryDate <= range.To.Date;
    }

    private void UpsertExpenseLog(ExpenseLogMemorySnapshot snapshot)
    {
        var existingIndex = _allExpenseLogs.FindIndex(log => log.Id == snapshot.ExpenseLogId);
        var vm = ToExpenseLogVm(snapshot);
        if (existingIndex >= 0)
            _allExpenseLogs[existingIndex] = vm;
        else
            _allExpenseLogs.Add(vm);
    }

    private void RemoveExpenseLog(int expenseLogId)
    {
        _allExpenseLogs = _allExpenseLogs
            .Where(log => log.Id != expenseLogId)
            .ToList();
    }

    private void UpsertIncomeLog(IncomeLogMemorySnapshot snapshot)
    {
        var existingIndex = _allIncomeLogs.FindIndex(log => log.Id == snapshot.IncomeLogId);
        var vm = ToIncomeLogVm(snapshot);
        if (existingIndex >= 0)
            _allIncomeLogs[existingIndex] = vm;
        else
            _allIncomeLogs.Add(vm);
    }

    private void RemoveIncomeLog(int incomeLogId)
    {
        _allIncomeLogs = _allIncomeLogs
            .Where(log => log.Id != incomeLogId)
            .ToList();
    }

    private void RefreshExpenseBucketsFromTrackedLogs()
    {
        _allExpenseLogs = _allExpenseLogs
            .Where(log => !log.IsForDeletion)
            .OrderByDescending(log => log.DeductedOn)
            .ToList();

        RefreshBudgetMetrics();
        ApplyVisibleExpenseLogs();
    }

    private static ExpenseLogMemorySnapshot CreateExpenseLogSnapshot(ExpenseLogVM expenseLog)
    {
        return new ExpenseLogMemorySnapshot(
            expenseLog.Expense?.Id ?? 0,
            expenseLog.Id,
            expenseLog.Expense?.Name ?? "Expense",
            expenseLog.Amount,
            expenseLog.Expense?.ExpenseKind ?? ExpenseKind.Manual,
            expenseLog.Expense?.ExpenseCategory ?? ExpenseCategory.Needs,
            expenseLog.Expense?.RecurringDate,
            expenseLog.Expense?.IsActive ?? false,
            expenseLog.SpendingSource?.Id ?? 0,
            expenseLog.Expense?.ExpenseTag?.Id ?? 0,
            expenseLog.DeductedOn,
            expenseLog.Notes,
            expenseLog.IsForDeletion);
    }

    private static ExpenseLogVM ToExpenseLogVm(ExpenseLogMemorySnapshot snapshot)
    {
        return new ExpenseLogVM
        {
            Id = snapshot.ExpenseLogId,
            Amount = snapshot.Amount,
            DeductedOn = snapshot.DeductedOn,
            Notes = snapshot.Notes,
            IsForDeletion = snapshot.IsForDeletion,
            SpendingSource = new SpendingSourceVM
            {
                Id = snapshot.SpendingSourceId
            },
            Expense = new ExpenseVM
            {
                Id = snapshot.ExpenseId,
                Name = snapshot.ExpenseName,
                Amount = snapshot.Amount,
                ExpenseKind = snapshot.ExpenseKind,
                ExpenseCategory = snapshot.ExpenseCategory,
                RecurringDate = snapshot.RecurringDate,
                IsActive = snapshot.IsActive,
                ExpenseTag = new ExpenseTagVM
                {
                    Id = snapshot.TagId
                }
            }
        };
    }

    private static IncomeLogVM ToIncomeLogVm(IncomeLogMemorySnapshot snapshot)
    {
        return new IncomeLogVM
        {
            Id = snapshot.IncomeLogId,
            Amount = snapshot.Amount,
            AddedOn = snapshot.AddedOn,
            Notes = snapshot.Notes,
            SpendingSource = new SpendingSourceVM
            {
                Id = snapshot.SpendingSourceId
            }
        };
    }

    private static void ReplaceExpenseLogs(ObservableCollection<ExpenseLogVM> target, IEnumerable<ExpenseLogVM> items)
    {
        target.Clear();

        foreach (var item in items.OrderByDescending(log => log.DeductedOn))
            target.Add(item);
    }
}
