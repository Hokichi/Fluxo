using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using AutoMapper;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Resources.Resources.Messages;
using Fluxo.Services.History;
using Fluxo.ViewModels.Entities;

namespace Fluxo.ViewModels.Shell.Main;

public partial class LedgerVM : ObservableRecipient,
    IRecipient<DateRangeSelectionChangedMessage>,
    IRecipient<AllTimeViewModeMessage>,
    IRecipient<LedgerSearchTextChangedMessage>
{
    private readonly IDataOperationRunner _dataOperationRunner;
    private readonly IExpenseLogService _expenseLogService;
    private readonly IMapper _mapper;
    private readonly IAccountService _accountService;
    private readonly ITagService _tagService;
    private readonly ObservableCollection<LedgerTransactionItemVM> _transactions = [];
    private readonly Dictionary<(LedgerTransactionKind Kind, int Id), BatchPreviewSnapshot> _batchPreviewSnapshots = [];
    private readonly SemaphoreSlim _reloadGate = new(1, 1);
    private LedgerFilterSelectionSnapshot _appliedFilterSelection = LedgerFilterSelectionSnapshot.Empty;
    private bool _isApplyingExternalRange;
    private bool _isSynchronizingFilters;
    private (DateTime From, DateTime To)? _selectedRange;

    [ObservableProperty] private LedgerGroupingMode _selectedGroupingMode = LedgerGroupingMode.Date;
    [ObservableProperty] private LedgerAmountSortDirection _amountSortDirection = LedgerAmountSortDirection.Descending;
    [ObservableProperty] private DateTime _startDate = DateTime.Today;
    [ObservableProperty] private DateTime _endDate = DateTime.Today;
    [ObservableProperty] private DateTime _maxSelectableDate = DateTime.Today;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private decimal _spentAmount;
    [ObservableProperty] private decimal _earnedAmount;
    [ObservableProperty] private decimal _goalAmount;
    [ObservableProperty] private decimal _netAmount;
    [ObservableProperty] private bool _hasTransactions;
    [ObservableProperty] private bool _hasVisibleTransactions;
    [ObservableProperty] private bool _isSelectionModeEnabled;
    [ObservableProperty] private bool _hasSelectedVisibleTransactions;
    [ObservableProperty] private bool _areAllVisibleTransactionsSelected;
    [ObservableProperty] private int? _selectedBatchAccountId;
    [ObservableProperty] private int? _selectedBatchTagId;
    [ObservableProperty] private LedgerTransactionItemVM? _editingTransaction;

    public LedgerVM(
        IExpenseLogService expenseLogService,
        IAccountService accountService,
        ITagService tagService,
        IDataOperationRunner dataOperationRunner,
        IMapper mapper,
        IMessenger? messenger = null)
        : this(
            expenseLogService,
            accountService,
            tagService,
            dataOperationRunner,
            mapper,
            new MainViewModeToggleVM(messenger ?? WeakReferenceMessenger.Default),
            messenger)
    {
    }

    public LedgerVM(
        IExpenseLogService expenseLogService,
        IAccountService accountService,
        ITagService tagService,
        IDataOperationRunner dataOperationRunner,
        IMapper mapper,
        MainViewModeToggleVM viewModeToggle,
        IMessenger? messenger = null)
        : base(messenger ?? WeakReferenceMessenger.Default)
    {
        _expenseLogService = expenseLogService;
        _accountService = accountService;
        _tagService = tagService;
        _dataOperationRunner = dataOperationRunner;
        _mapper = mapper;
        ViewModeToggle = viewModeToggle;
        TypeFilterPresentation = new LedgerFilterSelectionPresentation(
            "Type",
            () => TypeFilterSelectionCount,
            () => TypeFilterSelectionToolTip);
        AccountFilterPresentation = new LedgerFilterSelectionPresentation(
            "Account",
            () => AccountFilterSelectionCount,
            () => AccountFilterSelectionToolTip);
        CategoryFilterPresentation = new LedgerFilterSelectionPresentation(
            "Category",
            () => CategoryFilterSelectionCount,
            () => CategoryFilterSelectionToolTip);
        TagFilterPresentation = new LedgerFilterSelectionPresentation(
            "Tag",
            () => TagFilterSelectionCount,
            () => TagFilterSelectionToolTip);

        TransactionsView = CollectionViewSource.GetDefaultView(_transactions);
        TransactionsView.Filter = FilterTransaction;
        UpdateSortAndGroups();

        IsActive = true;
    }

    public MainViewModeToggleVM ViewModeToggle { get; }
    public ICollectionView TransactionsView { get; }
    public string EmptyStatePeriodText => BuildSelectedPeriodText();
    public ObservableCollection<LedgerFilterOption<LedgerTransactionKind>> TypeFilters { get; } = [];
    public ObservableCollection<LedgerFilterOption<int>> AccountFilters { get; } = [];
    public ObservableCollection<LedgerFilterOption<ExpenseCategory>> CategoryFilters { get; } = [];
    public ObservableCollection<LedgerFilterOption<int>> TagFilters { get; } = [];
    public ObservableCollection<ExpenseTagVM> EditableTags { get; } = [];
    public IReadOnlyList<LedgerFilterOption<int>> BatchAccountOptions =>
        AccountFilters.Where(option => !option.IsAll).ToList();
    public IReadOnlyList<LedgerFilterOption<int>> BatchTagOptions =>
        TagFilters.Where(option => !option.IsAll).ToList();
    public bool HasPendingFilterChanges => CaptureFilterSelectionSnapshot() != _appliedFilterSelection;
    public LedgerFilterSelectionPresentation TypeFilterPresentation { get; }
    public LedgerFilterSelectionPresentation AccountFilterPresentation { get; }
    public LedgerFilterSelectionPresentation CategoryFilterPresentation { get; }
    public LedgerFilterSelectionPresentation TagFilterPresentation { get; }
    public int TypeFilterSelectionCount => CountSpecificSelections(TypeFilters);
    public int AccountFilterSelectionCount => CountSpecificSelections(AccountFilters);
    public int CategoryFilterSelectionCount => CountSpecificSelections(CategoryFilters);
    public int TagFilterSelectionCount => CountSpecificSelections(TagFilters);
    public string? TypeFilterSelectionToolTip => BuildSpecificSelectionToolTip(TypeFilters);
    public string? AccountFilterSelectionToolTip => BuildSpecificSelectionToolTip(AccountFilters);
    public string? CategoryFilterSelectionToolTip => BuildSpecificSelectionToolTip(CategoryFilters);
    public string? TagFilterSelectionToolTip => BuildSpecificSelectionToolTip(TagFilters);
    public string SelectionModeButtonText => IsSelectionModeEnabled ? "Disable Selection" : "Enable Selection";
    public string CheckAllButtonText => AreAllVisibleTransactionsSelected ? "Uncheck All" : "Check All";
    public string DeleteSelectedButtonText => AreAllVisibleTransactionsSelected ? "Delete All" : "Delete Selected";
    public string BatchAccountSelectionText => SelectedBatchAccountId is { } sourceId
        ? BatchAccountOptions.FirstOrDefault(option => option.Value == sourceId)?.Label ?? string.Empty
        : string.Empty;
    public string BatchTagSelectionText => SelectedBatchTagId is { } tagId
        ? BatchTagOptions.FirstOrDefault(option => option.Value == tagId)?.Label ?? string.Empty
        : string.Empty;
    public IReadOnlyList<LedgerGroupingMode> GroupingModes { get; } =
    [
        LedgerGroupingMode.None,
        LedgerGroupingMode.Date,
        LedgerGroupingMode.Tags,
        LedgerGroupingMode.Accounts,
        LedgerGroupingMode.Types,
        LedgerGroupingMode.Category
    ];

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await _reloadGate.WaitAsync(cancellationToken);
        try
        {
            await ReloadPeriodAsync(cancellationToken);
        }
        finally
        {
            _reloadGate.Release();
        }
    }

    public void Receive(DateRangeSelectionChangedMessage message)
    {
        ApplyExternalDateRange(message.Value.From, message.Value.To, refresh: true);
    }

    public void ApplyExternalDateRange(DateTime from, DateTime to, bool refresh)
    {
        SetSelectedRange(from, to, updateSelectors: true);

        if (refresh)
            _ = LoadAsync();
    }

    public void Receive(AllTimeViewModeMessage message)
    {
        ApplyAllTimeRange(refresh: true);
    }

    public void ApplyAllTimeRange(bool refresh)
    {
        _selectedRange = null;
        OnPropertyChanged(nameof(EmptyStatePeriodText));
        _isApplyingExternalRange = true;
        try
        {
            StartDate = DateTime.Today;
            EndDate = DateTime.Today;
        }
        finally
        {
            _isApplyingExternalRange = false;
        }

        if (refresh)
            _ = LoadAsync();
    }

    public void Receive(LedgerSearchTextChangedMessage message)
    {
        SearchText = message.Value;
    }

    [RelayCommand]
    private void ToggleAmountSortDirection()
    {
        AmountSortDirection = AmountSortDirection == LedgerAmountSortDirection.Descending
            ? LedgerAmountSortDirection.Ascending
            : LedgerAmountSortDirection.Descending;
    }

    [RelayCommand]
    private async Task EditTransactionAsync(LedgerTransactionItemVM? transaction)
    {
        if (transaction is null || transaction.IsGoal)
            return;

        if (!transaction.IsEditing)
        {
            if (EditingTransaction is not null && !ReferenceEquals(EditingTransaction, transaction))
                return;

            transaction.IsEditing = true;
            EditingTransaction = transaction;
            RefreshEditDisabledState();
            return;
        }

        await CommitTransactionEditAsync(transaction);
        transaction.IsEditing = false;
        EditingTransaction = null;
        RefreshEditDisabledState();
    }

    [RelayCommand]
    private async Task RemoveTransactionAsync(LedgerTransactionItemVM? transaction)
    {
        if (transaction is null || transaction.IsGoal || transaction.IsEditing || transaction.IsDisabledByAnotherEdit)
            return;

        switch (transaction.Kind)
        {
            case LedgerTransactionKind.Expense:
                await RemoveExpenseTransactionAsync(transaction);
                break;
            case LedgerTransactionKind.Income:
                await RemoveIncomeTransactionAsync(transaction);
                break;
        }
    }

    [RelayCommand]
    private void ToggleSelectionMode()
    {
        var shouldEnable = !IsSelectionModeEnabled;
        if (!shouldEnable)
            RestoreAllBatchPreviewSnapshots();

        IsSelectionModeEnabled = shouldEnable;
        SelectedBatchAccountId = null;
        SelectedBatchTagId = null;
        if (IsSelectionModeEnabled)
            _batchPreviewSnapshots.Clear();

        foreach (var transaction in _transactions)
            transaction.IsSelectedForBatch = IsSelectionModeEnabled && TransactionsView.Contains(transaction);

        RefreshBatchSelectionState();
        RefreshEditDisabledState();
    }

    [RelayCommand]
    private void ToggleVisibleBatchSelection()
    {
        var visibleTransactions = GetVisibleTransactions();
        var shouldCheck = visibleTransactions.Count > 0 &&
                          !visibleTransactions.All(transaction => transaction.IsSelectedForBatch);

        foreach (var transaction in visibleTransactions)
            transaction.IsSelectedForBatch = shouldCheck;

        RefreshBatchSelectionState();
    }

    partial void OnSearchTextChanged(string value)
    {
        TransactionsView.Refresh();
        RefreshVisibleTransactionState();
    }

    partial void OnStartDateChanged(DateTime value)
    {
        if (_isApplyingExternalRange)
            return;

        SetSelectedRange(value, EndDate, updateSelectors: true);
        _ = LoadAsync();
    }

    partial void OnEndDateChanged(DateTime value)
    {
        if (_isApplyingExternalRange)
            return;

        SetSelectedRange(StartDate, value, updateSelectors: true);
        _ = LoadAsync();
    }

    partial void OnSelectedGroupingModeChanged(LedgerGroupingMode value)
    {
        UpdateSortAndGroups();
        RefreshVisibleTransactionState();
    }

    partial void OnAmountSortDirectionChanged(LedgerAmountSortDirection value)
    {
        UpdateSortAndGroups();
        RefreshVisibleTransactionState();
    }

    partial void OnIsSelectionModeEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(SelectionModeButtonText));
    }

    partial void OnSelectedBatchAccountIdChanged(int? value)
    {
        OnPropertyChanged(nameof(BatchAccountSelectionText));
        ApplyBatchPreview();
    }

    partial void OnSelectedBatchTagIdChanged(int? value)
    {
        OnPropertyChanged(nameof(BatchTagSelectionText));
        ApplyBatchPreview();
    }

    private async Task ReloadPeriodAsync(CancellationToken cancellationToken)
    {
        var expenseLogs = _mapper.Map<IReadOnlyList<ExpenseLogVM>>(
            await _expenseLogService.GetAllAsync(cancellationToken));
        var incomeLogs = await LoadIncomeLogsAsync(cancellationToken);
        var accounts = _mapper.Map<IReadOnlyList<AccountVM>>(
            await _accountService.GetAllAsync(cancellationToken));
        var tags = _mapper.Map<IReadOnlyList<ExpenseTagVM>>(
            await _tagService.GetAllAsync(cancellationToken));

        EditingTransaction = null;
        RebuildFilters(accounts, tags);

        var projected = expenseLogs
            .Where(log => !log.IsForDeletion)
            .Where(IsInSelectedRange)
            .Select(ProjectExpense)
            .Concat(incomeLogs.Where(IsInSelectedRange).Select(ProjectIncome))
            .OrderByDescending(item => item.OccurredOn)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _transactions.Clear();
        foreach (var transaction in projected)
            _transactions.Add(transaction);

        RefreshEditDisabledState();
        HasTransactions = _transactions.Count > 0;
        RefreshSummaries();
        TransactionsView.Refresh();
        RefreshVisibleTransactionState();
    }

    private void SetSelectedRange(DateTime from, DateTime to, bool updateSelectors)
    {
        var start = from.Date;
        var end = to.Date;
        var max = MaxSelectableDate.Date;

        if (start > max)
            start = max;
        if (end > max)
            end = max;
        if (start > end)
            (start, end) = (end, start);

        _selectedRange = (start, end);
        OnPropertyChanged(nameof(EmptyStatePeriodText));

        if (!updateSelectors)
            return;

        _isApplyingExternalRange = true;
        try
        {
            if (StartDate.Date != start)
                StartDate = start;
            if (EndDate.Date != end)
                EndDate = end;
        }
        finally
        {
            _isApplyingExternalRange = false;
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
                    Account = new AccountVM
                    {
                        Id = log.AccountId,
                        Name = log.Account?.Name ?? string.Empty,
                        AccountType = log.Account?.AccountType ?? AccountType.Checking,
                        IsEnabled = log.Account?.IsEnabled ?? true
                    }
                })
                .ToList();
        }, cancellationToken);
    }

    private void RebuildFilters(IReadOnlyList<AccountVM> accounts, IReadOnlyList<ExpenseTagVM> tags)
    {
        ReplaceEditableTags(tags);

        RebuildFilter(TypeFilters,
        [
            new LedgerFilterOption<LedgerTransactionKind>("All", default, isAll: true, isChecked: true),
            new LedgerFilterOption<LedgerTransactionKind>("Expenses", LedgerTransactionKind.Expense),
            new LedgerFilterOption<LedgerTransactionKind>("Incomes", LedgerTransactionKind.Income)
        ]);

        RebuildFilter(AccountFilters,
            new[] { new LedgerFilterOption<int>("All", default, isAll: true, isChecked: true) }
                .Concat(accounts
                    .OrderBy(source => source.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(source => new LedgerFilterOption<int>(source.Name, source.Id)))
                .ToList());

        RebuildFilter(CategoryFilters,
        [
            new LedgerFilterOption<ExpenseCategory>("All", default, isAll: true, isChecked: true),
            new LedgerFilterOption<ExpenseCategory>("Needs", ExpenseCategory.Needs),
            new LedgerFilterOption<ExpenseCategory>("Wants", ExpenseCategory.Wants),
            new LedgerFilterOption<ExpenseCategory>("Invest", ExpenseCategory.Savings)
        ]);

        RebuildFilter(TagFilters,
            new[] { new LedgerFilterOption<int>("All", default, isAll: true, isChecked: true) }
                .Concat(tags
                    .OrderBy(tag => tag.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(tag => new LedgerFilterOption<int>(tag.Name, tag.Id)))
                .ToList());

        _appliedFilterSelection = CaptureFilterSelectionSnapshot();
        OnPropertyChanged(nameof(BatchAccountOptions));
        OnPropertyChanged(nameof(BatchTagOptions));
        OnPropertyChanged(nameof(BatchAccountSelectionText));
        OnPropertyChanged(nameof(BatchTagSelectionText));
        RefreshAllFilterSelectionPresentations();
    }

    private void ReplaceEditableTags(IReadOnlyList<ExpenseTagVM> tags)
    {
        EditableTags.Clear();
        foreach (var tag in tags
                     .Where(tag => !tag.IsSystemTag)
                     .OrderBy(tag => tag.Name, StringComparer.OrdinalIgnoreCase))
        {
            EditableTags.Add(tag);
        }
    }

    private void RebuildFilter<T>(
        ObservableCollection<LedgerFilterOption<T>> target,
        IReadOnlyList<LedgerFilterOption<T>> options)
    {
        foreach (var option in target)
            option.PropertyChanged -= OnFilterOptionPropertyChanged;

        target.Clear();
        foreach (var option in options)
        {
            option.PropertyChanged += OnFilterOptionPropertyChanged;
            target.Add(option);
        }
    }

    private void OnFilterOptionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LedgerFilterOption<int>.IsChecked) || _isSynchronizingFilters)
            return;

        switch (sender)
        {
            case LedgerFilterOption<LedgerTransactionKind> type:
                NormalizeFilterSelection(TypeFilters, type);
                break;
            case LedgerFilterOption<int> source when AccountFilters.Contains(source):
                NormalizeFilterSelection(AccountFilters, source);
                break;
            case LedgerFilterOption<int> tag when TagFilters.Contains(tag):
                NormalizeFilterSelection(TagFilters, tag);
                break;
            case LedgerFilterOption<ExpenseCategory> category:
                NormalizeFilterSelection(CategoryFilters, category);
                break;
        }

        OnPropertyChanged(nameof(HasPendingFilterChanges));
        RefreshFilterSelectionPresentation(sender);
    }

    public void ApplyFilters()
    {
        _appliedFilterSelection = CaptureFilterSelectionSnapshot();
        TransactionsView.Refresh();
        RefreshVisibleTransactionState();
        OnPropertyChanged(nameof(HasPendingFilterChanges));
        RefreshAllFilterSelectionPresentations();
    }

    public bool ApplyFiltersIfChanged()
    {
        if (!HasPendingFilterChanges)
            return false;

        ApplyFilters();
        return true;
    }

    public IReadOnlyList<LedgerTransactionItemVM> GetVisibleTransactionsForExport()
    {
        var visibleTransactions = GetVisibleTransactions();
        return IsSelectionModeEnabled
            ? visibleTransactions.Where(transaction => transaction.IsSelectedForBatch).ToList()
            : visibleTransactions;
    }

    public void RefreshBatchSelectionState()
    {
        var visibleTransactions = GetVisibleTransactions();
        HasSelectedVisibleTransactions = visibleTransactions.Any(transaction => transaction.IsSelectedForBatch);
        AreAllVisibleTransactionsSelected = visibleTransactions.Count > 0 &&
                                            visibleTransactions.All(transaction => transaction.IsSelectedForBatch);
        OnPropertyChanged(nameof(CheckAllButtonText));
        OnPropertyChanged(nameof(DeleteSelectedButtonText));
        ApplyBatchPreview();
    }

    [RelayCommand]
    private async Task ApplyBatchTransactionUpdatesAsync()
    {
        var selectedTransactions = GetSelectedVisibleBatchTransactions();
        if (selectedTransactions.Count == 0 ||
            (SelectedBatchAccountId is null && SelectedBatchTagId is null))
        {
            return;
        }

        await _dataOperationRunner.RunAsync(async (scope, ct) =>
        {
            Account? targetSource = null;
            if (SelectedBatchAccountId is { } sourceId)
                targetSource = await scope.UnitOfWork.Accounts.GetByIdAsync(sourceId, ct);

            ExpenseTag? targetTag = null;
            if (SelectedBatchTagId is { } tagId)
                targetTag = await scope.UnitOfWork.ExpenseTags.GetByIdAsync(tagId, ct);

            foreach (var transaction in selectedTransactions)
            {
                if (transaction.Kind == LedgerTransactionKind.Expense)
                {
                    var expenseLog = await scope.UnitOfWork.ExpenseLogs.GetByIdAsync(transaction.Id, ct);
                    if (expenseLog?.Expense is null)
                        continue;

                    if (targetSource is not null)
                    {
                        expenseLog.Account = targetSource;
                        expenseLog.AccountId = targetSource.Id;
                        expenseLog.Expense.Account = targetSource;
                        expenseLog.Expense.AccountId = targetSource.Id;
                    }

                    if (targetTag is not null && !transaction.IsGoal)
                    {
                        expenseLog.Expense.ExpenseTag = targetTag;
                        expenseLog.Expense.ExpenseTagId = targetTag.Id;
                    }

                    scope.UnitOfWork.Expenses.Update(expenseLog.Expense);
                    scope.UnitOfWork.ExpenseLogs.Update(expenseLog);
                    continue;
                }

                if (targetSource is null)
                    continue;

                var incomeLog = await scope.UnitOfWork.IncomeLogs.GetByIdAsync(transaction.Id, ct);
                if (incomeLog is null)
                    continue;

                incomeLog.Account = targetSource;
                incomeLog.AccountId = targetSource.Id;
                scope.UnitOfWork.IncomeLogs.Update(incomeLog);
            }

            await scope.UnitOfWork.SaveChangesAsync(ct);
        });

        _batchPreviewSnapshots.Clear();
        SelectedBatchAccountId = null;
        SelectedBatchTagId = null;
        await LoadAsync();
        if (IsSelectionModeEnabled)
        {
            foreach (var transaction in GetVisibleTransactions())
                transaction.IsSelectedForBatch = true;
            RefreshBatchSelectionState();
        }
    }

    [RelayCommand]
    private async Task RemoveSelectedTransactionsAsync()
    {
        var selectedTransactions = GetSelectedVisibleBatchTransactions();
        if (selectedTransactions.Count == 0)
            return;

        await _dataOperationRunner.RunAsync(async (scope, ct) =>
        {
            foreach (var transaction in selectedTransactions)
            {
                if (transaction.Kind == LedgerTransactionKind.Expense)
                {
                    var expenseLog = await scope.UnitOfWork.ExpenseLogs.GetByIdAsync(transaction.Id, ct);
                    if (expenseLog is null)
                        continue;

                    expenseLog.IsForDeletion = true;
                    scope.UnitOfWork.ExpenseLogs.Update(expenseLog);
                    continue;
                }

                var incomeLog = await scope.UnitOfWork.IncomeLogs.GetByIdAsync(transaction.Id, ct);
                if (incomeLog is null)
                    continue;

                incomeLog.IsForDeletion = true;
                scope.UnitOfWork.IncomeLogs.Update(incomeLog);
            }

            await scope.UnitOfWork.SaveChangesAsync(ct);
        });

        foreach (var transaction in selectedTransactions)
            _transactions.Remove(transaction);

        _batchPreviewSnapshots.Clear();
        HasTransactions = _transactions.Count > 0;
        RefreshSummaries();
        TransactionsView.Refresh();
        RefreshVisibleTransactionState();
    }

    public void ClearFilters()
    {
        ResetFilter(TypeFilters);
        ResetFilter(AccountFilters);
        ResetFilter(CategoryFilters);
        ResetFilter(TagFilters);
        SearchText = string.Empty;
        ApplyFilters();
    }

    public void ApplyTagFilter(int tagId)
    {
        ApplySpecificFilter(TagFilters, tagId);
        ApplyFilters();
    }

    public void ApplyAccountFilter(int accountId)
    {
        ApplySpecificFilter(AccountFilters, accountId);
        ApplyFilters();
    }

    public void ApplyTransactionTag(LedgerTransactionItemVM? transaction, ExpenseTagVM? tag)
    {
        if (transaction is null ||
            tag is null ||
            tag.IsSystemTag ||
            transaction.Kind != LedgerTransactionKind.Expense ||
            !transaction.IsEditing)
        {
            return;
        }

        transaction.TagId = tag.Id;
        transaction.TagName = tag.Name;
        transaction.TagHexCode = tag.HexCode;
        transaction.IsTagPopupOpen = false;
    }

    private void NormalizeFilterSelection<T>(
        ObservableCollection<LedgerFilterOption<T>> options,
        LedgerFilterOption<T> changedOption)
    {
        var allOption = options.First(option => option.IsAll);
        var specificOptions = options.Where(option => !option.IsAll).ToList();

        _isSynchronizingFilters = true;
        try
        {
            if (changedOption.IsAll && changedOption.IsChecked)
            {
                foreach (var option in specificOptions)
                    option.IsChecked = false;
                return;
            }

            if (!changedOption.IsAll && changedOption.IsChecked)
                allOption.IsChecked = false;

            if (specificOptions.Count > 0 && specificOptions.All(option => option.IsChecked))
            {
                foreach (var option in specificOptions)
                    option.IsChecked = false;
                allOption.IsChecked = true;
                return;
            }

            if (!options.Any(option => option.IsChecked))
                allOption.IsChecked = true;
        }
        finally
        {
            _isSynchronizingFilters = false;
        }
    }

    private void ResetFilter<T>(ObservableCollection<LedgerFilterOption<T>> options)
    {
        _isSynchronizingFilters = true;
        try
        {
            foreach (var option in options)
                option.IsChecked = option.IsAll;
        }
        finally
        {
            _isSynchronizingFilters = false;
        }

        OnPropertyChanged(nameof(HasPendingFilterChanges));
        RefreshFilterSelectionPresentation(options);
    }

    private void ApplySpecificFilter<T>(ObservableCollection<LedgerFilterOption<T>> options, T value)
    {
        var selectedOption = options.FirstOrDefault(option =>
            !option.IsAll && EqualityComparer<T>.Default.Equals(option.Value, value));
        if (selectedOption is null)
            return;

        var allOption = options.First(option => option.IsAll);
        _isSynchronizingFilters = true;
        try
        {
            allOption.IsChecked = false;
            selectedOption.IsChecked = true;
        }
        finally
        {
            _isSynchronizingFilters = false;
        }

        NormalizeFilterSelection(options, selectedOption);
        OnPropertyChanged(nameof(HasPendingFilterChanges));
        RefreshFilterSelectionPresentation(options);
    }

    private static int CountSpecificSelections<T>(IEnumerable<LedgerFilterOption<T>> options)
    {
        return options.Count(option => !option.IsAll && option.IsChecked);
    }

    private static string? BuildSpecificSelectionToolTip<T>(IEnumerable<LedgerFilterOption<T>> options)
    {
        var labels = options
            .Where(option => !option.IsAll && option.IsChecked)
            .Select(option => option.Label)
            .ToList();

        return labels.Count == 0
            ? null
            : string.Join(Environment.NewLine, labels);
    }

    private void RefreshFilterSelectionPresentation(object? filterSource)
    {
        switch (filterSource)
        {
            case LedgerFilterOption<LedgerTransactionKind>:
            case ObservableCollection<LedgerFilterOption<LedgerTransactionKind>>:
                RefreshTypeFilterSelectionPresentation();
                break;
            case LedgerFilterOption<int> source when AccountFilters.Contains(source):
            case ObservableCollection<LedgerFilterOption<int>> sourceCollection when ReferenceEquals(sourceCollection, AccountFilters):
                RefreshAccountFilterSelectionPresentation();
                break;
            case LedgerFilterOption<int> tag when TagFilters.Contains(tag):
            case ObservableCollection<LedgerFilterOption<int>> tagCollection when ReferenceEquals(tagCollection, TagFilters):
                RefreshTagFilterSelectionPresentation();
                break;
            case LedgerFilterOption<ExpenseCategory>:
            case ObservableCollection<LedgerFilterOption<ExpenseCategory>>:
                RefreshCategoryFilterSelectionPresentation();
                break;
        }
    }

    private void RefreshAllFilterSelectionPresentations()
    {
        RefreshTypeFilterSelectionPresentation();
        RefreshAccountFilterSelectionPresentation();
        RefreshCategoryFilterSelectionPresentation();
        RefreshTagFilterSelectionPresentation();
    }

    private void RefreshTypeFilterSelectionPresentation()
    {
        OnPropertyChanged(nameof(TypeFilterSelectionCount));
        OnPropertyChanged(nameof(TypeFilterSelectionToolTip));
        TypeFilterPresentation.Refresh();
    }

    private void RefreshAccountFilterSelectionPresentation()
    {
        OnPropertyChanged(nameof(AccountFilterSelectionCount));
        OnPropertyChanged(nameof(AccountFilterSelectionToolTip));
        AccountFilterPresentation.Refresh();
    }

    private void RefreshCategoryFilterSelectionPresentation()
    {
        OnPropertyChanged(nameof(CategoryFilterSelectionCount));
        OnPropertyChanged(nameof(CategoryFilterSelectionToolTip));
        CategoryFilterPresentation.Refresh();
    }

    private void RefreshTagFilterSelectionPresentation()
    {
        OnPropertyChanged(nameof(TagFilterSelectionCount));
        OnPropertyChanged(nameof(TagFilterSelectionToolTip));
        TagFilterPresentation.Refresh();
    }

    private LedgerFilterSelectionSnapshot CaptureFilterSelectionSnapshot()
    {
        return new LedgerFilterSelectionSnapshot(
            CaptureFilterSelection(TypeFilters),
            CaptureFilterSelection(AccountFilters),
            CaptureFilterSelection(CategoryFilters),
            CaptureFilterSelection(TagFilters));
    }

    private static string CaptureFilterSelection<T>(IEnumerable<LedgerFilterOption<T>> options)
    {
        return string.Join(
            "|",
            options
                .Where(option => option.IsChecked)
                .Select(option => option.IsAll ? "all" : option.Value?.ToString() ?? string.Empty));
    }

    private bool FilterTransaction(object item)
    {
        if (item is not LedgerTransactionItemVM transaction)
            return false;

        if (!string.IsNullOrWhiteSpace(SearchText) &&
            transaction.Name.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase) is false)
            return false;

        if (!MatchesFilter(TypeFilters, transaction.Kind))
            return false;

        if (!MatchesFilter(AccountFilters, transaction.AccountId))
            return false;

        if (transaction.Kind == LedgerTransactionKind.Expense &&
            transaction.Category is { } category &&
            !MatchesFilter(CategoryFilters, category))
            return false;

        if (transaction.Kind == LedgerTransactionKind.Expense &&
            !MatchesFilter(TagFilters, transaction.TagId))
            return false;

        return true;
    }

    private static bool MatchesFilter<T>(IEnumerable<LedgerFilterOption<T>> options, T value)
    {
        var selectedOptions = options.Where(option => option.IsChecked).ToList();
        return selectedOptions.Any(option => option.IsAll) ||
               selectedOptions.Any(option => EqualityComparer<T>.Default.Equals(option.Value, value));
    }

    private bool IsInSelectedRange(ExpenseLogVM log)
    {
        return _selectedRange is not { } range ||
               log.DeductedOn.Date >= range.From.Date && log.DeductedOn.Date <= range.To.Date;
    }

    private bool IsInSelectedRange(IncomeLogVM log)
    {
        return _selectedRange is not { } range ||
               log.AddedOn.Date >= range.From.Date && log.AddedOn.Date <= range.To.Date;
    }

    private static LedgerTransactionItemVM ProjectExpense(ExpenseLogVM log)
    {
        var tagName = log.Expense?.ExpenseTag?.Name ?? string.Empty;
        return new LedgerTransactionItemVM
        {
            Id = log.Id,
            Kind = LedgerTransactionKind.Expense,
            Name = log.Expense?.Name ?? "Expense",
            Amount = log.Amount,
            OccurredOn = log.DeductedOn,
            Category = log.Expense?.ExpenseCategory ?? ExpenseCategory.Needs,
            AccountId = log.Account?.Id ?? 0,
            AccountName = log.Account?.Name ?? string.Empty,
            TagId = log.Expense?.ExpenseTag?.Id ?? 0,
            TagName = tagName,
            TagHexCode = log.Expense?.ExpenseTag?.HexCode ?? string.Empty,
            IsGoal = string.Equals(tagName, "Goal Update", StringComparison.OrdinalIgnoreCase),
            IsRecurring = log.Notes.Contains("recurring", StringComparison.OrdinalIgnoreCase)
        };
    }

    private static LedgerTransactionItemVM ProjectIncome(IncomeLogVM log)
    {
        return new LedgerTransactionItemVM
        {
            Id = log.Id,
            Kind = LedgerTransactionKind.Income,
            Name = log.Name,
            Amount = log.Amount,
            OccurredOn = log.AddedOn,
            AccountId = log.Account?.Id ?? 0,
            AccountName = log.Account?.Name ?? string.Empty
        };
    }

    private void UpdateSortAndGroups()
    {
        using (TransactionsView.DeferRefresh())
        {
            TransactionsView.GroupDescriptions.Clear();
            if (GetGroupPropertyName(SelectedGroupingMode) is { } groupPropertyName)
                TransactionsView.GroupDescriptions.Add(new PropertyGroupDescription(groupPropertyName));

            if (TransactionsView is ListCollectionView listCollectionView)
            {
                listCollectionView.CustomSort = new LedgerTransactionComparer(
                    SelectedGroupingMode,
                    AmountSortDirection);
                return;
            }

            TransactionsView.SortDescriptions.Clear();
            TransactionsView.SortDescriptions.Add(new SortDescription(
                nameof(LedgerTransactionItemVM.SignedAmount),
                AmountSortDirection == LedgerAmountSortDirection.Ascending
                    ? ListSortDirection.Ascending
                    : ListSortDirection.Descending));
        }
    }

    private static string? GetGroupPropertyName(LedgerGroupingMode groupingMode)
    {
        return groupingMode switch
        {
            LedgerGroupingMode.None => null,
            LedgerGroupingMode.Date => nameof(LedgerTransactionItemVM.DateGroupKey),
            LedgerGroupingMode.Tags => nameof(LedgerTransactionItemVM.TagGroupKey),
            LedgerGroupingMode.Accounts => nameof(LedgerTransactionItemVM.AccountGroupKey),
            LedgerGroupingMode.Types => nameof(LedgerTransactionItemVM.TypeGroupKey),
            LedgerGroupingMode.Category => nameof(LedgerTransactionItemVM.CategoryGroupKey),
            _ => null
        };
    }

    private void RefreshSummaries()
    {
        SpentAmount = _transactions
            .Where(transaction => transaction.Kind == LedgerTransactionKind.Expense && !transaction.IsGoal)
            .Sum(transaction => transaction.Amount);
        EarnedAmount = _transactions
            .Where(transaction => transaction.Kind == LedgerTransactionKind.Income)
            .Sum(transaction => transaction.Amount);
        GoalAmount = _transactions
            .Where(transaction => transaction.IsGoal)
            .Sum(transaction => transaction.Amount);
        NetAmount = EarnedAmount - SpentAmount - GoalAmount;
    }

    private Task CommitTransactionEditAsync(LedgerTransactionItemVM transaction)
    {
        return transaction.Kind switch
        {
            LedgerTransactionKind.Expense => CommitExpenseEditAsync(transaction),
            LedgerTransactionKind.Income => CommitIncomeEditAsync(transaction),
            _ => Task.CompletedTask
        };
    }

    private async Task CommitExpenseEditAsync(LedgerTransactionItemVM transaction)
    {
        var (before, after) = await _dataOperationRunner.RunAsync(async (scope, ct) =>
        {
            var expenseLog = await scope.UnitOfWork.ExpenseLogs.GetByIdAsync(transaction.Id, ct);
            if (expenseLog?.Expense is null)
                return ((ExpenseLogMemorySnapshot?)null, (ExpenseLogMemorySnapshot?)null);

            var beforeSnapshot = ExpenseLogMemorySnapshot.Create(expenseLog);
            var targetAccount =
                await scope.UnitOfWork.Accounts.GetByIdAsync(transaction.AccountId, ct) ??
                expenseLog.Account;
            var targetTag =
                await scope.UnitOfWork.ExpenseTags.GetByIdAsync(transaction.TagId, ct) ??
                expenseLog.Expense.ExpenseTag;

            expenseLog.Expense.Name = transaction.Name.Trim();
            expenseLog.Expense.Amount = transaction.Amount;
            expenseLog.Expense.Account = targetAccount;
            expenseLog.Expense.AccountId = targetAccount.Id;
            expenseLog.Expense.ExpenseTag = targetTag;
            expenseLog.Expense.ExpenseTagId = targetTag.Id;

            expenseLog.Amount = transaction.Amount;
            expenseLog.Account = targetAccount;
            expenseLog.AccountId = targetAccount.Id;

            scope.UnitOfWork.Expenses.Update(expenseLog.Expense);
            scope.UnitOfWork.ExpenseLogs.Update(expenseLog);
            await scope.UnitOfWork.SaveChangesAsync(ct);

            return (beforeSnapshot, ExpenseLogMemorySnapshot.Create(expenseLog));
        });

        if (before is null || after is null)
            return;

        ApplyExpenseSnapshotToTransaction(transaction, after);
        RefreshTransactionLookups(transaction);
        Messenger.Send(new RecordLogMemoryMessage(new EditExpenseLogMemoryAction(before, after)));
        RefreshSummaries();
        TransactionsView.Refresh();
        RefreshVisibleTransactionState();
    }

    private async Task CommitIncomeEditAsync(LedgerTransactionItemVM transaction)
    {
        var (before, after) = await _dataOperationRunner.RunAsync(async (scope, ct) =>
        {
            var incomeLog = await scope.UnitOfWork.IncomeLogs.GetByIdAsync(transaction.Id, ct);
            if (incomeLog is null)
                return ((IncomeLogMemorySnapshot?)null, (IncomeLogMemorySnapshot?)null);

            var beforeSnapshot = IncomeLogMemorySnapshot.Create(incomeLog);
            var targetAccount =
                await scope.UnitOfWork.Accounts.GetByIdAsync(transaction.AccountId, ct) ??
                incomeLog.Account;

            incomeLog.Name = transaction.Name.Trim();
            incomeLog.Amount = transaction.Amount;
            incomeLog.Account = targetAccount;
            incomeLog.AccountId = targetAccount.Id;

            scope.UnitOfWork.IncomeLogs.Update(incomeLog);
            await scope.UnitOfWork.SaveChangesAsync(ct);

            return (beforeSnapshot, IncomeLogMemorySnapshot.Create(incomeLog));
        });

        if (before is null || after is null)
            return;

        ApplyIncomeSnapshotToTransaction(transaction, after);
        RefreshTransactionLookups(transaction);
        Messenger.Send(new RecordLogMemoryMessage(new EditIncomeLogMemoryAction(before, after)));
        RefreshSummaries();
        TransactionsView.Refresh();
        RefreshVisibleTransactionState();
    }

    private async Task RemoveExpenseTransactionAsync(LedgerTransactionItemVM transaction)
    {
        var snapshot = await _dataOperationRunner.RunAsync(async (scope, ct) =>
        {
            var expenseLog = await scope.UnitOfWork.ExpenseLogs.GetByIdAsync(transaction.Id, ct);
            if (expenseLog is null)
                return null;

            var result = ExpenseLogMemorySnapshot.Create(expenseLog);
            expenseLog.IsForDeletion = true;
            scope.UnitOfWork.ExpenseLogs.Update(expenseLog);
            await scope.UnitOfWork.SaveChangesAsync(ct);
            return result;
        });

        if (snapshot is null)
            return;

        _transactions.Remove(transaction);
        HasTransactions = _transactions.Count > 0;
        Messenger.Send(new RecordLogMemoryMessage(new DeleteExpenseLogMemoryAction(snapshot)));
        RefreshSummaries();
        TransactionsView.Refresh();
        RefreshVisibleTransactionState();
    }

    private async Task RemoveIncomeTransactionAsync(LedgerTransactionItemVM transaction)
    {
        var snapshot = await _dataOperationRunner.RunAsync(async (scope, ct) =>
        {
            var incomeLog = await scope.UnitOfWork.IncomeLogs.GetByIdAsync(transaction.Id, ct);
            if (incomeLog is null)
                return null;

            var result = IncomeLogMemorySnapshot.Create(incomeLog);
            incomeLog.IsForDeletion = true;
            scope.UnitOfWork.IncomeLogs.Update(incomeLog);
            await scope.UnitOfWork.SaveChangesAsync(ct);
            return result;
        });

        if (snapshot is null)
            return;

        _transactions.Remove(transaction);
        HasTransactions = _transactions.Count > 0;
        Messenger.Send(new RecordLogMemoryMessage(new DeleteIncomeLogMemoryAction(snapshot)));
        RefreshSummaries();
        TransactionsView.Refresh();
        RefreshVisibleTransactionState();
    }

    private static void ApplyExpenseSnapshotToTransaction(
        LedgerTransactionItemVM transaction,
        ExpenseLogMemorySnapshot snapshot)
    {
        transaction.Name = snapshot.ExpenseName;
        transaction.Amount = snapshot.Amount;
        transaction.AccountId = snapshot.AccountId;
        transaction.TagId = snapshot.TagId;
    }

    private static void ApplyIncomeSnapshotToTransaction(
        LedgerTransactionItemVM transaction,
        IncomeLogMemorySnapshot snapshot)
    {
        transaction.Name = snapshot.Name;
        transaction.Amount = snapshot.Amount;
        transaction.AccountId = snapshot.AccountId;
    }

    private void RefreshTransactionLookups(LedgerTransactionItemVM transaction)
    {
        transaction.AccountName = AccountFilters
            .FirstOrDefault(option => !option.IsAll && option.Value == transaction.AccountId)
            ?.Label ?? transaction.AccountName;

        if (transaction.Kind != LedgerTransactionKind.Expense)
            return;

        var tag = TagFilters.FirstOrDefault(option => !option.IsAll && option.Value == transaction.TagId);
        if (tag is not null)
            transaction.TagName = tag.Label;

        var editableTag = EditableTags.FirstOrDefault(tag => tag.Id == transaction.TagId);
        if (editableTag is not null)
            transaction.TagHexCode = editableTag.HexCode;
    }

    private void RefreshVisibleTransactionState()
    {
        foreach (var transaction in _transactions)
            transaction.IsLastVisibleInGroup = false;

        var visibleTransactions = GetVisibleTransactions();
        HasVisibleTransactions = visibleTransactions.Count > 0;

        if (SelectedGroupingMode == LedgerGroupingMode.None)
        {
            if (visibleTransactions.LastOrDefault() is { } lastTransaction)
                lastTransaction.IsLastVisibleInGroup = true;
            RefreshBatchSelectionState();
            return;
        }

        foreach (var group in visibleTransactions.GroupBy(GetVisibleGroupKey))
        {
            if (group.LastOrDefault() is { } lastTransaction)
                lastTransaction.IsLastVisibleInGroup = true;
        }

        RefreshBatchSelectionState();
    }

    private IReadOnlyList<LedgerTransactionItemVM> GetVisibleTransactions()
    {
        return TransactionsView.Cast<LedgerTransactionItemVM>().ToList();
    }

    private IReadOnlyList<LedgerTransactionItemVM> GetSelectedVisibleBatchTransactions()
    {
        return GetVisibleTransactions()
            .Where(transaction => transaction.IsSelectedForBatch)
            .ToList();
    }

    private void ApplyBatchPreview()
    {
        if (!IsSelectionModeEnabled)
            return;

        if (SelectedBatchAccountId is null && SelectedBatchTagId is null)
        {
            RestoreAllBatchPreviewSnapshots();
            return;
        }

        var visibleTransactions = GetVisibleTransactions();
        foreach (var transaction in visibleTransactions.Where(transaction => !transaction.IsSelectedForBatch))
            RestoreBatchPreviewSnapshot(transaction);

        foreach (var transaction in visibleTransactions.Where(transaction => transaction.IsSelectedForBatch))
        {
            CaptureBatchPreviewSnapshot(transaction);

            if (SelectedBatchAccountId is { } sourceId)
            {
                transaction.AccountId = sourceId;
                transaction.AccountName = AccountFilters
                    .FirstOrDefault(option => !option.IsAll && option.Value == sourceId)
                    ?.Label ?? transaction.AccountName;
            }

            if (SelectedBatchTagId is { } tagId &&
                transaction.Kind == LedgerTransactionKind.Expense &&
                !transaction.IsGoal)
            {
                transaction.TagId = tagId;
                transaction.TagName = TagFilters
                    .FirstOrDefault(option => !option.IsAll && option.Value == tagId)
                    ?.Label ?? transaction.TagName;
                transaction.TagHexCode = EditableTags
                    .FirstOrDefault(tag => tag.Id == tagId)
                    ?.HexCode ?? transaction.TagHexCode;
            }
        }
    }

    private void CaptureBatchPreviewSnapshot(LedgerTransactionItemVM transaction)
    {
        var key = (transaction.Kind, transaction.Id);
        if (_batchPreviewSnapshots.ContainsKey(key))
            return;

        _batchPreviewSnapshots[key] = new BatchPreviewSnapshot(
            transaction.AccountId,
            transaction.AccountName,
            transaction.TagId,
            transaction.TagName,
            transaction.TagHexCode);
    }

    private void RestoreAllBatchPreviewSnapshots()
    {
        foreach (var transaction in _transactions)
            RestoreBatchPreviewSnapshot(transaction);

        _batchPreviewSnapshots.Clear();
    }

    private void RestoreBatchPreviewSnapshot(LedgerTransactionItemVM transaction)
    {
        var key = (transaction.Kind, transaction.Id);
        if (!_batchPreviewSnapshots.Remove(key, out var snapshot))
            return;

        transaction.AccountId = snapshot.AccountId;
        transaction.AccountName = snapshot.AccountName;
        transaction.TagId = snapshot.TagId;
        transaction.TagName = snapshot.TagName;
        transaction.TagHexCode = snapshot.TagHexCode;
    }

    private string GetVisibleGroupKey(LedgerTransactionItemVM transaction)
    {
        return SelectedGroupingMode switch
        {
            LedgerGroupingMode.Date => transaction.DateGroupKey,
            LedgerGroupingMode.Tags => transaction.TagGroupKey,
            LedgerGroupingMode.Accounts => transaction.AccountGroupKey,
            LedgerGroupingMode.Types => transaction.TypeGroupKey,
            LedgerGroupingMode.Category => transaction.CategoryGroupKey,
            _ => string.Empty
        };
    }

    private void RefreshEditDisabledState()
    {
        foreach (var transaction in _transactions)
        {
            transaction.IsTagPopupOpen = transaction.IsEditing && transaction.IsTagPopupOpen;
            transaction.IsDisabledByAnotherEdit =
                EditingTransaction is not null && !ReferenceEquals(EditingTransaction, transaction);
        }
    }

    private string BuildSelectedPeriodText()
    {
        if (_selectedRange is not { } range)
            return "all time";

        var start = range.From.Date;
        var end = range.To.Date;
        var dayRange = start == end
            ? DateRangeResolver.Resolve(start, MainContentViewMode.Daily)
            : null;
        var from = dayRange?.From ?? start;
        var to = dayRange?.To ?? end;

        return from.Date == to.Date
            ? FormatPeriodDate(from)
            : $"{FormatPeriodDate(from)} to {FormatPeriodDate(to)}";
    }

    private static string FormatPeriodDate(DateTime date)
    {
        return date.ToString("MMMM d", CultureInfo.InvariantCulture);
    }

    private sealed class LedgerTransactionComparer(
        LedgerGroupingMode groupingMode,
        LedgerAmountSortDirection amountSortDirection)
        : IComparer
    {
        public int Compare(object? x, object? y)
        {
            if (ReferenceEquals(x, y))
                return 0;
            if (x is not LedgerTransactionItemVM left)
                return -1;
            if (y is not LedgerTransactionItemVM right)
                return 1;

            var groupComparison = CompareGroup(left, right);
            if (groupComparison != 0)
                return groupComparison;

            var amountComparison = left.SignedAmount.CompareTo(right.SignedAmount);
            if (amountSortDirection == LedgerAmountSortDirection.Descending)
                amountComparison *= -1;
            if (amountComparison != 0)
                return amountComparison;

            return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        }

        private int CompareGroup(LedgerTransactionItemVM left, LedgerTransactionItemVM right)
        {
            return groupingMode switch
            {
                LedgerGroupingMode.Date => right.OccurredOn.Date.CompareTo(left.OccurredOn.Date),
                LedgerGroupingMode.Tags => string.Compare(left.TagGroupKey, right.TagGroupKey, StringComparison.OrdinalIgnoreCase),
                LedgerGroupingMode.Accounts => string.Compare(left.AccountGroupKey, right.AccountGroupKey, StringComparison.OrdinalIgnoreCase),
                LedgerGroupingMode.Types => string.Compare(left.TypeGroupKey, right.TypeGroupKey, StringComparison.OrdinalIgnoreCase),
                LedgerGroupingMode.Category => string.Compare(left.CategoryGroupKey, right.CategoryGroupKey, StringComparison.OrdinalIgnoreCase),
                _ => 0
            };
        }
    }

    private readonly record struct LedgerFilterSelectionSnapshot(
        string Type,
        string Account,
        string Category,
        string Tag)
    {
        public static LedgerFilterSelectionSnapshot Empty { get; } = new(string.Empty, string.Empty, string.Empty, string.Empty);
    }

    private readonly record struct BatchPreviewSnapshot(
        int AccountId,
        string AccountName,
        int TagId,
        string TagName,
        string TagHexCode);
}

public sealed class LedgerGroupingModeDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            LedgerGroupingMode.Accounts => "Accounts",
            LedgerGroupingMode groupingMode => groupingMode.ToString(),
            _ => string.Empty
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public sealed class LedgerFilterSelectionPresentation(
    string label,
    Func<int> selectionCountProvider,
    Func<string?> selectionToolTipProvider)
    : ObservableObject
{
    public string Label { get; } = label;
    public int SelectionCount => selectionCountProvider();
    public string? SelectionToolTip => selectionToolTipProvider();

    public void Refresh()
    {
        OnPropertyChanged(nameof(SelectionCount));
        OnPropertyChanged(nameof(SelectionToolTip));
    }
}
