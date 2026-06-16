using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using AutoMapper;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Resources.Resources.Messages;
using Fluxo.Services.Dialogs;
using Fluxo.Services.History;
using Fluxo.Services.Ui;
using Fluxo.ViewModels.Entities;
using CoreILogMemoryAction = Fluxo.Core.Interfaces.History.ILogMemoryAction;

namespace Fluxo.ViewModels.Shell.Main;

public partial class BudgetAllocationPanelVM : ObservableRecipient,
    IRecipient<DateRangeSelectionChangedMessage>,
    IRecipient<AllTimeViewModeMessage>,
    IRecipient<DashboardDataInvalidatedMessage>,
    IRecipient<RecordLogMemoryMessage>,
    IRecipient<LogMemoryActionAppliedMessage>
{
    private const int BucketPageSize = 25;
    private const int VisibleTagSlots = 5;

    private readonly IDataOperationRunner _dataOperationRunner;
    private readonly IExpenseLogService _expenseLogService;
    private readonly HashSet<ExpenseLogVM> _investVisibleWindow = [];
    private readonly Dictionary<int, ExpenseTagVM> _knownTagsById = [];
    private readonly IMapper _mapper;
    private readonly ObservableCollection<ExpenseLogVM> _investSource = [];
    private readonly HashSet<ExpenseLogVM> _needsVisibleWindow = [];
    private readonly ObservableCollection<ExpenseLogVM> _needsSource = [];
    private readonly SemaphoreSlim _reloadGate = new(1, 1);
    private readonly ISpendingSourceService _spendingSourceService;
    private readonly ITagService _tagService;
    private readonly HashSet<ExpenseLogVM> _wantsVisibleWindow = [];
    private readonly ObservableCollection<ExpenseLogVM> _wantsSource = [];
    private readonly ObservableCollection<SpendingSourceVM> _spendingSources = [];
    private readonly IDialogService? _dialogService;
    private readonly IUiSettleAwaiter? _uiSettleAwaiter;
    private readonly SemaphoreSlim _filterFeedbackGate = new(1, 1);
    private readonly List<ExpenseTagVM> _orderedTags = [];
    private readonly AllocationDataVM? _allocationData;

    private List<ExpenseLogVM> _allExpenseLogs = [];
    private List<IncomeLogVM> _allIncomeLogs = [];
    private BudgetAllocation _budgetAllocation = new();
    private int _investVisibleCount = BucketPageSize;
    private bool _isSynchronizingTagSelections;
    private bool _suppressFilterFeedback;
    private int _needsVisibleCount = BucketPageSize;
    private (DateTime From, DateTime To)? _selectedRange = (DateTime.Today, DateTime.Today);
    private int _wantsVisibleCount = BucketPageSize;

    public BudgetAllocationPanelVM(
        IExpenseLogService expenseLogService,
        ISpendingSourceService spendingSourceService,
        ITagService tagService,
        IDataOperationRunner dataOperationRunner,
        IMapper mapper,
        IMessenger? messenger = null,
        IDialogService? dialogService = null,
        IUiSettleAwaiter? uiSettleAwaiter = null,
        AllocationDataVM? allocationData = null)
        : base(messenger ?? WeakReferenceMessenger.Default)
    {
        _expenseLogService = expenseLogService;
        _spendingSourceService = spendingSourceService;
        _tagService = tagService;
        _dataOperationRunner = dataOperationRunner;
        _mapper = mapper;
        _dialogService = dialogService;
        _uiSettleAwaiter = uiSettleAwaiter;
        _allocationData = allocationData;

        if (_allocationData is not null)
            _allocationData.PropertyChanged += OnAllocationDataPropertyChanged;

        Initialize();
    }

    public decimal TotalSpent => _allocationData?.TotalSpent ?? 0m;

    public int DailyAllowance => _allocationData?.DailyAllowance ?? 0;

    public decimal NeedsAvailable => _allocationData?.NeedsAvailable ?? 0m;

    public decimal WantsAvailable => _allocationData?.WantsAvailable ?? 0m;

    public decimal InvestAvailable => _allocationData?.InvestAvailable ?? 0m;

    public decimal NeedsSpent => _allocationData?.NeedsSpent ?? 0m;

    public decimal WantsSpent => _allocationData?.WantsSpent ?? 0m;

    public decimal InvestSpent => _allocationData?.InvestSpent ?? 0m;

    public decimal NeedsRemaining => _allocationData?.NeedsRemaining ?? 0m;

    public decimal WantsRemaining => _allocationData?.WantsRemaining ?? 0m;

    public decimal InvestRemaining => _allocationData?.InvestRemaining ?? 0m;

    public int NeedsPercentage => _allocationData?.NeedsPercentage ?? 0;

    public int WantsPercentage => _allocationData?.WantsPercentage ?? 0;

    public int InvestPercentage => _allocationData?.InvestPercentage ?? 0;

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
    private bool _needsHasMoreItems;

    [ObservableProperty]
    private bool _wantsHasMoreItems;

    [ObservableProperty]
    private bool _investHasMoreItems;

    [ObservableProperty]
    private bool _isNeedsLoading;

    [ObservableProperty]
    private bool _isWantsLoading;

    [ObservableProperty]
    private bool _isInvestLoading;

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

    [ObservableProperty]
    private decimal _needsThreshold = 0.5m;

    [ObservableProperty]
    private decimal _wantsThreshold = 0.3m;

    [ObservableProperty]
    private decimal _investThreshold = 0.2m;

    public int NeedsAllocationPercentage => ConvertThresholdToPercentage(NeedsThreshold);

    public int WantsAllocationPercentage => ConvertThresholdToPercentage(WantsThreshold);

    public int InvestAllocationPercentage => ConvertThresholdToPercentage(InvestThreshold);

    public bool HasOtherTags => OtherTags.Count > 0;

    public bool IsSelectedTagInOtherTags => SelectedOtherTag is not null;

    public ObservableCollection<SpendingSourceVM> SpendingSources => _spendingSources;

    public decimal TotalIncomeAmount => _allocationData?.TotalIncomeAmount ?? 0m;

    public IReadOnlyList<ExpenseLogVM> GetAllExpenseLogs() => _allExpenseLogs.ToList();

    public IReadOnlyList<IncomeLogVM> GetAllIncomeLogs() => _allIncomeLogs.ToList();

    public void Receive(DateRangeSelectionChangedMessage message)
    {
        _selectedRange = message.Value;
        RefreshRangeScopedData();
    }

    public void Receive(AllTimeViewModeMessage message)
    {
        _selectedRange = null;
        RefreshRangeScopedData();
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
        _budgetAllocation = await LoadBudgetAllocationAsync(cancellationToken);
        NeedsThreshold = _budgetAllocation.NeedsThreshold / 100m;
        WantsThreshold = _budgetAllocation.WantsThreshold / 100m;
        InvestThreshold = _budgetAllocation.InvestThreshold / 100m;

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

        if (_allocationData is not null)
            await _allocationData.LoadAsync(cancellationToken);

        _spendingSources.Clear();
        foreach (var source in spendingSources)
            _spendingSources.Add(source);

        if (SelectedSpendingSourceId is int selectedId &&
            _spendingSources.All(source => source.Id != selectedId))
            SetSelectedSpendingSourceInternal(null);
        else
            SynchronizeSpendingSourceSelections(SelectedSpendingSourceId);

        CacheKnownTags(tags);
        ApplyVisibleExpenseLogs();
        RefreshSourceDifferences();
    }

    partial void OnSelectedTagChanged(ExpenseTagVM? value)
    {
        if (!_isSynchronizingTagSelections &&
            value is not null &&
            OtherTags.Any(tag => tag.Id == value.Id))
            PromoteTagToVisibleStart(value);
        else
            SynchronizeTagSelections(value);

        OnPropertyChanged(nameof(IsSelectedTagInOtherTags));
        ResetPaginationWindows();

        if (_suppressFilterFeedback)
        {
            RefreshExpenseViews();
            return;
        }

        QueueFilterRefreshWithFeedback($"Filtering {value?.Name ?? "All"}");
    }

    partial void OnSelectedSpendingSourceIdChanged(int? value)
    {
        SynchronizeSpendingSourceSelections(value);
        ResetPaginationWindows();

        if (_suppressFilterFeedback)
        {
            RefreshExpenseViews();
            return;
        }

        var sourceName = value is int sourceId
            ? Enumerable.FirstOrDefault<SpendingSourceVM>(_spendingSources, source => source.Id == sourceId)?.Name
            : null;
        QueueFilterRefreshWithFeedback($"Filtering for {sourceName ?? "All sources"}");
    }

    partial void OnNeedsThresholdChanged(decimal value)
    {
        OnPropertyChanged(nameof(NeedsAllocationPercentage));
    }

    partial void OnWantsThresholdChanged(decimal value)
    {
        OnPropertyChanged(nameof(WantsAllocationPercentage));
    }

    partial void OnInvestThresholdChanged(decimal value)
    {
        OnPropertyChanged(nameof(InvestAllocationPercentage));
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

    partial void OnNeedsHasMoreItemsChanged(bool value)
    {
        LoadMoreNeedsCommand.NotifyCanExecuteChanged();
    }

    partial void OnWantsHasMoreItemsChanged(bool value)
    {
        LoadMoreWantsCommand.NotifyCanExecuteChanged();
    }

    partial void OnInvestHasMoreItemsChanged(bool value)
    {
        LoadMoreInvestCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsNeedsLoadingChanged(bool value)
    {
        LoadMoreNeedsCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsWantsLoadingChanged(bool value)
    {
        LoadMoreWantsCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsInvestLoadingChanged(bool value)
    {
        LoadMoreInvestCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void ClearSelectedTag()
    {
        SelectedTag = null;
    }

    [RelayCommand(CanExecute = nameof(CanLoadMoreNeeds))]
    private void LoadMoreNeeds()
    {
        if (!CanLoadMoreNeeds())
            return;

        IsNeedsLoading = true;
        try
        {
            _needsVisibleCount += BucketPageSize;
            RefreshExpenseViews();
        }
        finally
        {
            IsNeedsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanLoadMoreWants))]
    private void LoadMoreWants()
    {
        if (!CanLoadMoreWants())
            return;

        IsWantsLoading = true;
        try
        {
            _wantsVisibleCount += BucketPageSize;
            RefreshExpenseViews();
        }
        finally
        {
            IsWantsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanLoadMoreInvest))]
    private void LoadMoreInvest()
    {
        if (!CanLoadMoreInvest())
            return;

        IsInvestLoading = true;
        try
        {
            _investVisibleCount += BucketPageSize;
            RefreshExpenseViews();
        }
        finally
        {
            IsInvestLoading = false;
        }
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

    private void SetSelectedSpendingSourceInternal(int? sourceId)
    {
        _suppressFilterFeedback = true;
        try
        {
            SelectedSpendingSourceId = sourceId;
        }
        finally
        {
            _suppressFilterFeedback = false;
        }
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
        ResetPaginationWindows();
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
                    Name = log.Name,
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

    private void CacheKnownTags(IEnumerable<ExpenseTagVM> allTags)
    {
        _knownTagsById.Clear();
        foreach (var tag in allTags.Where(tag => tag.Id > 0))
            _knownTagsById[tag.Id] = CloneTag(tag);
    }

    private void ConfigureExpenseViews()
    {
        Needs = CollectionViewSource.GetDefaultView(_needsSource);
        Wants = CollectionViewSource.GetDefaultView(_wantsSource);
        Invest = CollectionViewSource.GetDefaultView(_investSource);

        Needs.Filter = FilterNeedsExpenseLog;
        Wants.Filter = FilterWantsExpenseLog;
        Invest.Filter = FilterInvestExpenseLog;
    }

    private async Task<BudgetAllocation> LoadBudgetAllocationAsync(CancellationToken cancellationToken)
    {
        return await _dataOperationRunner.RunAsync(async (scope, ct) =>
            await scope.UnitOfWork.BudgetAllocation.GetAsync(ct) ?? new BudgetAllocation(), cancellationToken);
    }

    private static int ConvertThresholdToPercentage(decimal threshold)
    {
        return (int)Math.Round(threshold * 100m, MidpointRounding.AwayFromZero);
    }

    private void ApplyVisibleExpenseLogs(bool resetPaginationWindows = true)
    {
        var visibleExpenseLogs = (_selectedRange is { } range
            ? _allExpenseLogs.Where(log => log.DeductedOn.Date >= range.From.Date && log.DeductedOn.Date <= range.To.Date)
            : _allExpenseLogs)
            .ToList();

        ReplaceExpenseLogs(
            _needsSource,
            visibleExpenseLogs.Where(log => log.Expense?.ExpenseCategory == ExpenseCategory.Needs));
        ReplaceExpenseLogs(
            _wantsSource,
            visibleExpenseLogs.Where(log => log.Expense?.ExpenseCategory == ExpenseCategory.Wants));
        ReplaceExpenseLogs(
            _investSource,
            visibleExpenseLogs.Where(log => log.Expense?.ExpenseCategory == ExpenseCategory.Savings));
        LoadRangeTags(visibleExpenseLogs);

        if (resetPaginationWindows)
            ResetPaginationWindows();

        RefreshExpenseViews();
    }

    private bool FilterNeedsExpenseLog(object item)
    {
        return FilterPagedExpenseLog(item, _needsVisibleWindow);
    }

    private bool FilterWantsExpenseLog(object item)
    {
        return FilterPagedExpenseLog(item, _wantsVisibleWindow);
    }

    private bool FilterInvestExpenseLog(object item)
    {
        return FilterPagedExpenseLog(item, _investVisibleWindow);
    }

    private bool FilterPagedExpenseLog(object item, HashSet<ExpenseLogVM> visibleWindow)
    {
        if (item is not ExpenseLogVM expenseLog)
            return false;

        if (!MatchesSelectedFilters(expenseLog))
            return false;

        return visibleWindow.Contains(expenseLog);
    }

    private bool MatchesSelectedFilters(ExpenseLogVM expenseLog)
    {
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

    private void PromoteTagToVisibleStart(ExpenseTagVM selectedTag)
    {
        var promotedTag = _orderedTags.FirstOrDefault(tag => tag.Id == selectedTag.Id) ?? selectedTag;
        var reorderedTags = _orderedTags
            .Where(tag => tag.Id != selectedTag.Id)
            .Prepend(promotedTag)
            .ToList();

        _orderedTags.Clear();
        _orderedTags.AddRange(reorderedTags);

        RefreshTagCollections();
        SynchronizeTagSelections(promotedTag);
    }

    private void QueueFilterRefreshWithFeedback(string message)
    {
        QueueFilterRefreshWithFeedback(message, RefreshExpenseViews);
    }

    private void QueueFilterRefreshWithFeedback(string message, Action refreshOperation)
    {
        _ = ApplyFilterFeedbackAsync(message, refreshOperation);
    }

    private async Task ApplyFilterFeedbackAsync(string message, Action refreshOperation)
    {
        if (_dialogService is null || _uiSettleAwaiter is null)
        {
            refreshOperation();
            return;
        }

        await _filterFeedbackGate.WaitAsync();
        try
        {
            await _dialogService.ShowToastWhileAsync(
                message,
                async () =>
                {
                    refreshOperation();
                    await _uiSettleAwaiter.WaitForUiReadyAsync();
                });
        }
        catch
        {
            refreshOperation();
        }
        finally
        {
            _filterFeedbackGate.Release();
        }
    }

    private void RefreshRangeScopedData()
    {
        ApplyVisibleExpenseLogs();
        RefreshSourceDifferences();
    }

    private void RefreshExpenseViews()
    {
        RecomputePagedVisibleWindows();

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

    private void ApplyBalanceImpactsFromAction(CoreILogMemoryAction action, LogMemoryApplyDirection direction)
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

            case EditIncomeLogMemoryAction editIncomeAction:
                ApplyEditedIncomeAction(editIncomeAction, direction);
                return;

            case DeleteIncomeLogMemoryAction deleteIncomeAction:
                ApplyDeletedIncomeAction(deleteIncomeAction, direction);
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
            ApplyExpenseToTrackedSource(snapshot);
            AdjustSourceDifference(snapshot.SpendingSourceId, snapshot.DeductedOn, snapshot.Amount);
            RefreshExpenseBucketsFromTrackedLogs();
            return;
        }

        RemoveExpenseLog(snapshot.ExpenseLogId);
        RestoreExpenseFromTrackedSource(snapshot);
        AdjustSourceDifference(snapshot.SpendingSourceId, snapshot.DeductedOn, -snapshot.Amount);
        RefreshExpenseBucketsFromTrackedLogs();
    }

    private void ApplyIncomeAction(IncomeLogMemorySnapshot snapshot, LogMemoryApplyDirection direction)
    {
        if (direction == LogMemoryApplyDirection.Redo)
        {
            UpsertIncomeLog(snapshot);
            ApplyIncomeToTrackedSource(snapshot);
            AdjustSourceDifference(snapshot.SpendingSourceId, snapshot.AddedOn, -snapshot.Amount);
            return;
        }

        RemoveIncomeLog(snapshot.IncomeLogId);
        RestoreIncomeFromTrackedSource(snapshot);
        AdjustSourceDifference(snapshot.SpendingSourceId, snapshot.AddedOn, snapshot.Amount);
    }

    private void ApplyEditedExpenseAction(EditExpenseLogMemoryAction action, LogMemoryApplyDirection direction)
    {
        var previous = direction == LogMemoryApplyDirection.Redo ? action.Before : action.After;
        var target = direction == LogMemoryApplyDirection.Redo ? action.After : action.Before;

        UpsertExpenseLog(target);
        RestoreExpenseFromTrackedSource(previous);
        ApplyExpenseToTrackedSource(target);
        AdjustSourceDifference(previous.SpendingSourceId, previous.DeductedOn, -previous.Amount);
        AdjustSourceDifference(target.SpendingSourceId, target.DeductedOn, target.Amount);
        RefreshExpenseBucketsFromTrackedLogs();
    }

    private void ApplyEditedIncomeAction(EditIncomeLogMemoryAction action, LogMemoryApplyDirection direction)
    {
        var previous = direction == LogMemoryApplyDirection.Redo ? action.Before : action.After;
        var target = direction == LogMemoryApplyDirection.Redo ? action.After : action.Before;

        UpsertIncomeLog(target);
        RestoreIncomeFromTrackedSource(previous);
        ApplyIncomeToTrackedSource(target);
        AdjustSourceDifference(previous.SpendingSourceId, previous.AddedOn, previous.Amount);
        AdjustSourceDifference(target.SpendingSourceId, target.AddedOn, -target.Amount);
    }

    private void ApplyDeletedExpenseAction(DeleteExpenseLogMemoryAction action, LogMemoryApplyDirection direction)
    {
        if (action.Snapshot is not { } snapshot)
            return;

        if (direction == LogMemoryApplyDirection.Redo)
        {
            RemoveExpenseLog(snapshot.ExpenseLogId);
            RestoreExpenseFromTrackedSource(snapshot);
            AdjustSourceDifference(snapshot.SpendingSourceId, snapshot.DeductedOn, snapshot.Amount);
            RefreshExpenseBucketsFromTrackedLogs();
            return;
        }

        UpsertExpenseLog(snapshot);
        ApplyExpenseToTrackedSource(snapshot);
        AdjustSourceDifference(snapshot.SpendingSourceId, snapshot.DeductedOn, -snapshot.Amount);
        RefreshExpenseBucketsFromTrackedLogs();
    }

    private void ApplyDeletedIncomeAction(DeleteIncomeLogMemoryAction action, LogMemoryApplyDirection direction)
    {
        var snapshot = action.Snapshot;

        if (direction == LogMemoryApplyDirection.Redo)
        {
            RemoveIncomeLog(snapshot.IncomeLogId);
            RestoreIncomeFromTrackedSource(snapshot);
            AdjustSourceDifference(snapshot.SpendingSourceId, snapshot.AddedOn, snapshot.Amount);
            return;
        }

        UpsertIncomeLog(snapshot);
        ApplyIncomeToTrackedSource(snapshot);
        AdjustSourceDifference(snapshot.SpendingSourceId, snapshot.AddedOn, -snapshot.Amount);
    }

    private void ApplyExpenseToTrackedSource(ExpenseLogMemorySnapshot snapshot)
    {
        var source = _spendingSources.FirstOrDefault(candidate => candidate.Id == snapshot.SpendingSourceId);
        if (source is null)
            return;

        if (source.SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL)
        {
            source.SpentAmount += snapshot.Amount;
            return;
        }

        source.Balance -= snapshot.Amount;
    }

    private void RestoreExpenseFromTrackedSource(ExpenseLogMemorySnapshot snapshot)
    {
        var source = _spendingSources.FirstOrDefault(candidate => candidate.Id == snapshot.SpendingSourceId);
        if (source is null)
            return;

        if (source.SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL)
        {
            source.SpentAmount = Math.Max(0m, source.SpentAmount - snapshot.Amount);
            return;
        }

        source.Balance += snapshot.Amount;
    }

    private void ApplyIncomeToTrackedSource(IncomeLogMemorySnapshot snapshot)
    {
        var source = _spendingSources.FirstOrDefault(candidate => candidate.Id == snapshot.SpendingSourceId);
        if (source is null)
            return;

        if (source.SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL)
        {
            source.SpentAmount = Math.Max(0m, source.SpentAmount - snapshot.Amount);
            return;
        }

        source.Balance += snapshot.Amount;
    }

    private void RestoreIncomeFromTrackedSource(IncomeLogMemorySnapshot snapshot)
    {
        var source = _spendingSources.FirstOrDefault(candidate => candidate.Id == snapshot.SpendingSourceId);
        if (source is null)
            return;

        if (source.SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL)
        {
            source.SpentAmount += snapshot.Amount;
            return;
        }

        source.Balance -= snapshot.Amount;
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
        var existing = existingIndex >= 0 ? _allExpenseLogs[existingIndex] : null;
        var vm = ToExpenseLogVm(snapshot, existing);
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

        ApplyVisibleExpenseLogs(resetPaginationWindows: false);
    }

    private void OnAllocationDataPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(e.PropertyName))
            OnPropertyChanged(e.PropertyName);
    }

    private bool CanLoadMoreNeeds()
    {
        return NeedsHasMoreItems && !IsNeedsLoading;
    }

    private bool CanLoadMoreWants()
    {
        return WantsHasMoreItems && !IsWantsLoading;
    }

    private bool CanLoadMoreInvest()
    {
        return InvestHasMoreItems && !IsInvestLoading;
    }

    private void ResetPaginationWindows()
    {
        _needsVisibleCount = BucketPageSize;
        _wantsVisibleCount = BucketPageSize;
        _investVisibleCount = BucketPageSize;

        IsNeedsLoading = false;
        IsWantsLoading = false;
        IsInvestLoading = false;
    }

    private void RecomputePagedVisibleWindows()
    {
        NeedsHasMoreItems = UpdatePagedVisibleWindow(_needsSource, _needsVisibleWindow, _needsVisibleCount);
        WantsHasMoreItems = UpdatePagedVisibleWindow(_wantsSource, _wantsVisibleWindow, _wantsVisibleCount);
        InvestHasMoreItems = UpdatePagedVisibleWindow(_investSource, _investVisibleWindow, _investVisibleCount);
    }

    private bool UpdatePagedVisibleWindow(
        IEnumerable<ExpenseLogVM> source,
        HashSet<ExpenseLogVM> visibleWindow,
        int visibleCount)
    {
        var filteredLogs = source
            .Where(MatchesSelectedFilters)
            .ToList();

        visibleWindow.Clear();
        foreach (var log in filteredLogs.Take(visibleCount))
            visibleWindow.Add(log);

        return filteredLogs.Count > visibleCount;
    }

    private void LoadRangeTags(IEnumerable<ExpenseLogVM> visibleExpenseLogs)
    {
        var orderedTags = BuildOrderedRangeTags(visibleExpenseLogs).ToList();

        _orderedTags.Clear();
        _orderedTags.AddRange(orderedTags);

        RefreshTagCollections();

        if (SelectedTag is not null &&
            orderedTags.All(tag => tag.Id != SelectedTag.Id))
            SetSelectedTagInternal(null);
        else
            SynchronizeTagSelections(SelectedTag);
    }

    private void RefreshTagCollections()
    {
        Tags = new ObservableCollection<ExpenseTagVM>(_orderedTags.Take(VisibleTagSlots));
        OtherTags = new ObservableCollection<ExpenseTagVM>(_orderedTags.Skip(VisibleTagSlots));

        OnPropertyChanged(nameof(HasOtherTags));
        OnPropertyChanged(nameof(IsSelectedTagInOtherTags));
    }

    private IEnumerable<ExpenseTagVM> BuildOrderedRangeTags(IEnumerable<ExpenseLogVM> visibleExpenseLogs)
    {
        return visibleExpenseLogs
            .Where(log => !log.IsForDeletion)
            .Select(log => log.Expense?.ExpenseTag)
            .Where(tag => tag is { Id: > 0 })
            .GroupBy(tag => tag!.Id)
            .Select(group =>
            {
                var sourceTag = _knownTagsById.GetValueOrDefault(group.Key) ?? group.First()!;
                return new
                {
                    Tag = CloneTag(sourceTag),
                    UsageCount = group.Count()
                };
            })
            .OrderBy(item => item.Tag.IsSystemTag)
            .ThenByDescending(item => item.UsageCount)
            .ThenBy(item => item.Tag.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => item.Tag);
    }

    private void SetSelectedTagInternal(ExpenseTagVM? selectedTag)
    {
        _suppressFilterFeedback = true;
        try
        {
            SelectedTag = selectedTag;
        }
        finally
        {
            _suppressFilterFeedback = false;
        }
    }

    private static ExpenseTagVM CloneTag(ExpenseTagVM source)
    {
        return new ExpenseTagVM
        {
            Id = source.Id,
            Name = source.Name,
            HexCode = source.HexCode,
            IsSystemTag = source.IsSystemTag,
            SpendingLimit = source.SpendingLimit
        };
    }

    private static ExpenseLogMemorySnapshot CreateExpenseLogSnapshot(ExpenseLogVM expenseLog)
    {
        return new ExpenseLogMemorySnapshot(
            expenseLog.Expense?.Id ?? 0,
            expenseLog.Id,
            expenseLog.Expense?.Name ?? "Expense",
            expenseLog.Amount,
            expenseLog.Expense?.ExpenseCategory ?? ExpenseCategory.Needs,
            expenseLog.SpendingSource?.Id ?? 0,
            expenseLog.Expense?.ExpenseTag?.Id ?? 0,
            expenseLog.DeductedOn,
            expenseLog.Notes,
            expenseLog.IsForDeletion);
    }

    private ExpenseLogVM ToExpenseLogVm(ExpenseLogMemorySnapshot snapshot, ExpenseLogVM? existing)
    {
        var knownSpendingSource = _spendingSources.FirstOrDefault(source => source.Id == snapshot.SpendingSourceId);
        var existingSpendingSource = existing?.SpendingSource;
        var resolvedSpendingSourceType = knownSpendingSource?.SpendingSourceType ??
                                         existingSpendingSource?.SpendingSourceType ??
                                         SpendingSourceType.Checking;

        var knownTag = _knownTagsById.GetValueOrDefault(snapshot.TagId);
        var existingTag = existing?.Expense?.ExpenseTag;

        return new ExpenseLogVM
        {
            Id = snapshot.ExpenseLogId,
            Amount = snapshot.Amount,
            DeductedOn = snapshot.DeductedOn,
            Notes = snapshot.Notes,
            IsForDeletion = snapshot.IsForDeletion,
            SpendingSource = new SpendingSourceVM
            {
                Id = snapshot.SpendingSourceId,
                Name = knownSpendingSource?.Name ?? existingSpendingSource?.Name ?? string.Empty,
                SpendingSourceType = resolvedSpendingSourceType
            },
            Expense = new ExpenseVM
            {
                Id = snapshot.ExpenseId,
                Name = snapshot.ExpenseName,
                Amount = snapshot.Amount,
                ExpenseCategory = snapshot.ExpenseCategory,
                ExpenseTag = new ExpenseTagVM
                {
                    Id = snapshot.TagId,
                    Name = knownTag?.Name ?? existingTag?.Name ?? string.Empty,
                    HexCode = knownTag?.HexCode ?? existingTag?.HexCode ?? string.Empty,
                    IsSystemTag = knownTag?.IsSystemTag ?? existingTag?.IsSystemTag ?? false,
                    SpendingLimit = knownTag?.SpendingLimit ?? existingTag?.SpendingLimit
                }
            }
        };
    }

    private static IncomeLogVM ToIncomeLogVm(IncomeLogMemorySnapshot snapshot)
    {
        return new IncomeLogVM
        {
            Id = snapshot.IncomeLogId,
            Name = snapshot.Name,
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
