using System.Collections.ObjectModel;
using System.ComponentModel;
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
    private readonly ISpendingSourceService _spendingSourceService;
    private readonly ITagService _tagService;
    private readonly ObservableCollection<LedgerTransactionItemVM> _transactions = [];
    private readonly SemaphoreSlim _reloadGate = new(1, 1);
    private bool _isSynchronizingFilters;
    private (DateTime From, DateTime To)? _selectedRange;

    [ObservableProperty] private LedgerGroupingMode _selectedGroupingMode = LedgerGroupingMode.Date;
    [ObservableProperty] private LedgerAmountSortDirection _amountSortDirection = LedgerAmountSortDirection.Descending;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private decimal _spentAmount;
    [ObservableProperty] private decimal _earnedAmount;
    [ObservableProperty] private decimal _goalAmount;
    [ObservableProperty] private decimal _netAmount;

    public LedgerVM(
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

        TransactionsView = CollectionViewSource.GetDefaultView(_transactions);
        TransactionsView.Filter = FilterTransaction;
        UpdateSortAndGroups();

        IsActive = true;
    }

    public ICollectionView TransactionsView { get; }
    public ObservableCollection<LedgerFilterOption<LedgerTransactionKind>> TypeFilters { get; } = [];
    public ObservableCollection<LedgerFilterOption<int>> SpendingSourceFilters { get; } = [];
    public ObservableCollection<LedgerFilterOption<ExpenseCategory>> CategoryFilters { get; } = [];
    public ObservableCollection<LedgerFilterOption<int>> TagFilters { get; } = [];
    public IReadOnlyList<LedgerGroupingMode> GroupingModes { get; } =
    [
        LedgerGroupingMode.None,
        LedgerGroupingMode.Date,
        LedgerGroupingMode.Tags,
        LedgerGroupingMode.SpendingSources,
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
        _selectedRange = message.Value;
        _ = LoadAsync();
    }

    public void Receive(AllTimeViewModeMessage message)
    {
        _selectedRange = null;
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
            transaction.IsEditing = true;
            return;
        }

        await CommitTransactionEditAsync(transaction);
        transaction.IsEditing = false;
    }

    [RelayCommand]
    private async Task RemoveTransactionAsync(LedgerTransactionItemVM? transaction)
    {
        if (transaction is null || transaction.IsGoal)
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

    partial void OnSearchTextChanged(string value)
    {
        TransactionsView.Refresh();
    }

    partial void OnSelectedGroupingModeChanged(LedgerGroupingMode value)
    {
        UpdateSortAndGroups();
    }

    partial void OnAmountSortDirectionChanged(LedgerAmountSortDirection value)
    {
        UpdateSortAndGroups();
    }

    private async Task ReloadPeriodAsync(CancellationToken cancellationToken)
    {
        var expenseLogs = _mapper.Map<IReadOnlyList<ExpenseLogVM>>(
            await _expenseLogService.GetAllAsync(cancellationToken));
        var incomeLogs = await LoadIncomeLogsAsync(cancellationToken);
        var spendingSources = _mapper.Map<IReadOnlyList<SpendingSourceVM>>(
            await _spendingSourceService.GetAllAsync(cancellationToken));
        var tags = _mapper.Map<IReadOnlyList<ExpenseTagVM>>(
            await _tagService.GetAllAsync(cancellationToken));

        RebuildFilters(spendingSources, tags);

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

        RefreshSummaries();
        TransactionsView.Refresh();
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
                        SpendingSourceType = log.SpendingSource?.SpendingSourceType ?? SpendingSourceType.Checking,
                        IsEnabled = log.SpendingSource?.IsEnabled ?? true
                    }
                })
                .ToList();
        }, cancellationToken);
    }

    private void RebuildFilters(IReadOnlyList<SpendingSourceVM> spendingSources, IReadOnlyList<ExpenseTagVM> tags)
    {
        RebuildFilter(TypeFilters,
        [
            new LedgerFilterOption<LedgerTransactionKind>("All", default, isAll: true, isChecked: true),
            new LedgerFilterOption<LedgerTransactionKind>("Expenses", LedgerTransactionKind.Expense),
            new LedgerFilterOption<LedgerTransactionKind>("Incomes", LedgerTransactionKind.Income)
        ]);

        RebuildFilter(SpendingSourceFilters,
            new[] { new LedgerFilterOption<int>("All", default, isAll: true, isChecked: true) }
                .Concat(spendingSources
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
            case LedgerFilterOption<int> source when SpendingSourceFilters.Contains(source):
                NormalizeFilterSelection(SpendingSourceFilters, source);
                break;
            case LedgerFilterOption<int> tag when TagFilters.Contains(tag):
                NormalizeFilterSelection(TagFilters, tag);
                break;
            case LedgerFilterOption<ExpenseCategory> category:
                NormalizeFilterSelection(CategoryFilters, category);
                break;
        }

        TransactionsView.Refresh();
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

    private bool FilterTransaction(object item)
    {
        if (item is not LedgerTransactionItemVM transaction)
            return false;

        if (!string.IsNullOrWhiteSpace(SearchText) &&
            transaction.Name.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase) is false)
            return false;

        if (!MatchesFilter(TypeFilters, transaction.Kind))
            return false;

        if (!MatchesFilter(SpendingSourceFilters, transaction.SpendingSourceId))
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
            SpendingSourceId = log.SpendingSource?.Id ?? 0,
            SpendingSourceName = log.SpendingSource?.Name ?? string.Empty,
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
            SpendingSourceId = log.SpendingSource?.Id ?? 0,
            SpendingSourceName = log.SpendingSource?.Name ?? string.Empty
        };
    }

    private void UpdateSortAndGroups()
    {
        using (TransactionsView.DeferRefresh())
        {
            TransactionsView.GroupDescriptions.Clear();
            if (GetGroupPropertyName(SelectedGroupingMode) is { } groupPropertyName)
                TransactionsView.GroupDescriptions.Add(new PropertyGroupDescription(groupPropertyName));

            TransactionsView.SortDescriptions.Clear();
            if (SelectedGroupingMode == LedgerGroupingMode.None)
                TransactionsView.SortDescriptions.Add(new SortDescription(
                    nameof(LedgerTransactionItemVM.OccurredOn),
                    ListSortDirection.Descending));

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
            LedgerGroupingMode.SpendingSources => nameof(LedgerTransactionItemVM.SpendingSourceGroupKey),
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
            var targetSpendingSource =
                await scope.UnitOfWork.SpendingSources.GetByIdAsync(transaction.SpendingSourceId, ct) ??
                expenseLog.SpendingSource;
            var targetTag =
                await scope.UnitOfWork.ExpenseTags.GetByIdAsync(transaction.TagId, ct) ??
                expenseLog.Expense.ExpenseTag;

            expenseLog.Expense.Name = transaction.Name.Trim();
            expenseLog.Expense.Amount = transaction.Amount;
            expenseLog.Expense.SpendingSource = targetSpendingSource;
            expenseLog.Expense.SpendingSourceId = targetSpendingSource.Id;
            expenseLog.Expense.ExpenseTag = targetTag;
            expenseLog.Expense.ExpenseTagId = targetTag.Id;

            expenseLog.Amount = transaction.Amount;
            expenseLog.SpendingSource = targetSpendingSource;
            expenseLog.SpendingSourceId = targetSpendingSource.Id;

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
    }

    private async Task CommitIncomeEditAsync(LedgerTransactionItemVM transaction)
    {
        var (before, after) = await _dataOperationRunner.RunAsync(async (scope, ct) =>
        {
            var incomeLog = await scope.UnitOfWork.IncomeLogs.GetByIdAsync(transaction.Id, ct);
            if (incomeLog is null)
                return ((IncomeLogMemorySnapshot?)null, (IncomeLogMemorySnapshot?)null);

            var beforeSnapshot = IncomeLogMemorySnapshot.Create(incomeLog);
            var targetSpendingSource =
                await scope.UnitOfWork.SpendingSources.GetByIdAsync(transaction.SpendingSourceId, ct) ??
                incomeLog.SpendingSource;

            incomeLog.Name = transaction.Name.Trim();
            incomeLog.Amount = transaction.Amount;
            incomeLog.SpendingSource = targetSpendingSource;
            incomeLog.SpendingSourceId = targetSpendingSource.Id;

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
        Messenger.Send(new RecordLogMemoryMessage(new DeleteExpenseLogMemoryAction(snapshot)));
        RefreshSummaries();
        TransactionsView.Refresh();
    }

    private async Task RemoveIncomeTransactionAsync(LedgerTransactionItemVM transaction)
    {
        var snapshot = await _dataOperationRunner.RunAsync(async (scope, ct) =>
        {
            var incomeLog = await scope.UnitOfWork.IncomeLogs.GetByIdAsync(transaction.Id, ct);
            if (incomeLog is null)
                return null;

            var result = IncomeLogMemorySnapshot.Create(incomeLog);
            scope.UnitOfWork.IncomeLogs.Remove(incomeLog);
            await scope.UnitOfWork.SaveChangesAsync(ct);
            return result;
        });

        if (snapshot is null)
            return;

        _transactions.Remove(transaction);
        Messenger.Send(new RecordLogMemoryMessage(new DeleteIncomeLogMemoryAction(snapshot)));
        RefreshSummaries();
        TransactionsView.Refresh();
    }

    private static void ApplyExpenseSnapshotToTransaction(
        LedgerTransactionItemVM transaction,
        ExpenseLogMemorySnapshot snapshot)
    {
        transaction.Name = snapshot.ExpenseName;
        transaction.Amount = snapshot.Amount;
        transaction.SpendingSourceId = snapshot.SpendingSourceId;
        transaction.TagId = snapshot.TagId;
    }

    private static void ApplyIncomeSnapshotToTransaction(
        LedgerTransactionItemVM transaction,
        IncomeLogMemorySnapshot snapshot)
    {
        transaction.Name = snapshot.Name;
        transaction.Amount = snapshot.Amount;
        transaction.SpendingSourceId = snapshot.SpendingSourceId;
    }

    private void RefreshTransactionLookups(LedgerTransactionItemVM transaction)
    {
        transaction.SpendingSourceName = SpendingSourceFilters
            .FirstOrDefault(option => !option.IsAll && option.Value == transaction.SpendingSourceId)
            ?.Label ?? transaction.SpendingSourceName;

        if (transaction.Kind != LedgerTransactionKind.Expense)
            return;

        var tag = TagFilters.FirstOrDefault(option => !option.IsAll && option.Value == transaction.TagId);
        if (tag is not null)
            transaction.TagName = tag.Label;
    }
}
