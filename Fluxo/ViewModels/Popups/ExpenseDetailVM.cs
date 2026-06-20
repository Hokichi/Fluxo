using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Constants;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Resources.Resources.Messages;
using Fluxo.Services.History;
using Fluxo.Services.Logging;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Popups.Helpers;
using Fluxo.ViewModels.Shell;
using MainVM = Fluxo.ViewModels.Shell.Main.MainVM;

namespace Fluxo.ViewModels.Popups;

public partial class ExpenseDetailVM : ObservableObject
{
    private const int DefaultVisibleTagSlots = 4;
    private readonly List<AccountVM> _availableAccounts = [];
    private readonly ExpenseLogVM _expenseLog;
    private readonly MainVM _mainViewModel;
    private readonly List<ExpenseTagVM> _orderedTags = [];
    private readonly List<ExpenseSplitRowVM> _removedSplitRows = [];
    private readonly IAppDataService _appData;

    [ObservableProperty] private decimal _amountText;
    [ObservableProperty] private bool _isPinned;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private bool _isSplitMode;
    [ObservableProperty] private bool _isMoreTagsOpen;
    [ObservableProperty] private bool _hasNegativeSplitRemainder;
    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private ExpenseSplitRowVM? _negativeRemainderRow;
    private bool _isApplyingSplitRemainder;
    private bool _isUpdatingTagCollections;
    private int _visibleTagSlots = DefaultVisibleTagSlots;
    [ObservableProperty] private string _nameText = string.Empty;
    [ObservableProperty] private string _noteText = string.Empty;
    [ObservableProperty] private string _popupTitle = "Expense Detail";

    private ExpenseDetailSavedState _savedState = new(string.Empty, 0m, false, string.Empty, DateTime.Today,
        ExpenseCategory.Needs, 0, 0);

    [ObservableProperty] private DateTime _selectedDate = DateTime.Today;
    [ObservableProperty] private ExpenseCategory _selectedExpenseCategory = ExpenseCategory.Needs;
    [ObservableProperty] private AccountVM? _selectedAccount;
    [ObservableProperty] private ExpenseTagVM? _selectedTag;

    public ExpenseDetailVM(MainVM mainViewModel, ExpenseLogVM expenseLog, IAppDataService appData)
    {
        _mainViewModel = mainViewModel;
        _expenseLog = expenseLog;
        _appData = appData;
        AccountsView = AccountComboBoxViewFactory.CreateGroupedByTypeThenName(
            Accounts,
            nameof(AccountVM.TypeDisplayName),
            nameof(AccountVM.AccountType),
            nameof(AccountVM.Name));

        ReloadChoicesFromMainViewModel();
        _savedState = CreateSavedState(expenseLog);
        LoadFromSavedState();
    }

    public IReadOnlyList<ExpenseCategoryOption> ExpenseCategories { get; } =
    [
        new("Needs", ExpenseCategory.Needs),
        new("Wants", ExpenseCategory.Wants),
        new("Invest", ExpenseCategory.Savings)
    ];

    public ObservableCollection<AccountVM> Accounts { get; } = [];
    public ICollectionView AccountsView { get; }
    public ObservableCollection<ExpenseSplitRowVM> SplitRows { get; } = [];
    public ObservableCollection<ExpenseDetailChildTransactionVM> ChildTransactions { get; } = [];
    public ObservableCollection<ExpenseTagVM> VisibleTags { get; } = [];
    public ObservableCollection<ExpenseTagVM> OverflowTags { get; } = [];

    public bool AreFieldsReadOnly => !IsEditing;
    public bool CanEditFields => IsEditing;
    public bool HasMoreTags => OverflowTags.Count > 0;
    public bool HasSplitRows => SplitRows.Count > 0;
    public bool HasSplitRowsWithAmounts => SplitRows.Any(row => row.HasAmount);
    public bool HasSplitRowsWithoutAmounts => SplitRows.Count > 0 && SplitRows.All(row => !row.HasAmount);
    public bool HasChildTransactions => ChildTransactions.Count > 0;
    public bool ShowChildTransactions => HasChildTransactions && !IsSplitMode;
    public int DetailPopupWidth => ShowChildTransactions ? 916 : 640;
    public bool ShowSplitButton => !IsSplitMode;
    public bool ShowNormalExpenseFields => !IsSplitMode;
    public IEnumerable<ExpenseTagVM> AllSplitTags => _orderedTags.Where(tag => !tag.IsSystemTag);
    public bool HasSplitParentRemainder => IsSplitMode && AmountText > 0m;
    public bool CanCloseSplitModeWithoutSaving => IsSplitMode && !HasSplitRows;
    public bool RequiresEmptySplitConfirmationOnClose => IsSplitMode && HasSplitRowsWithoutAmounts;

    partial void OnIsEditingChanged(bool value)
    {
        OnPropertyChanged(nameof(AreFieldsReadOnly));
        OnPropertyChanged(nameof(CanEditFields));
        RefreshTagCollections();

        if (!value)
            IsMoreTagsOpen = false;
    }

    partial void OnIsSplitModeChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowSplitButton));
        OnPropertyChanged(nameof(ShowNormalExpenseFields));
        OnPropertyChanged(nameof(ShowChildTransactions));
        OnPropertyChanged(nameof(DetailPopupWidth));
        OnPropertyChanged(nameof(HasSplitParentRemainder));
        OnPropertyChanged(nameof(CanCloseSplitModeWithoutSaving));
        OnPropertyChanged(nameof(RequiresEmptySplitConfirmationOnClose));
    }

    partial void OnAmountTextChanged(decimal value)
    {
        OnPropertyChanged(nameof(HasSplitParentRemainder));

        if (!IsSplitMode || _isApplyingSplitRemainder)
            return;

        UpdateSplitNegativeRemainderState(value, SplitRows.LastOrDefault(row => row.HasAmount));
    }

    partial void OnSelectedTagChanged(ExpenseTagVM? value)
    {
        if (_isUpdatingTagCollections || value is null)
            return;

        if (!IsEditing)
        {
            RefreshTagCollections();
            return;
        }

        if (OverflowTags.Any(tag => tag.Id == value.Id))
            PromoteTagToVisibleStart(value);

        IsMoreTagsOpen = false;
    }

    public async Task BeginEditingAsync()
    {
        IsEditing = true;
        await EnsureTagsLoadedAsync();
    }

    public void CancelEditing()
    {
        IsEditing = false;
        ClearSplitMode();
        LoadFromSavedState();
    }

    public void BeginSplitMode()
    {
        IsEditing = true;
        IsSplitMode = true;
        HasNegativeSplitRemainder = false;
        NegativeRemainderRow = null;
    }

    public async Task BeginSplitModeAsync(CancellationToken cancellationToken = default)
    {
        BeginSplitMode();
        await LoadChildTransactionsIntoSplitRowsAsync(cancellationToken);
    }

    public void AddSplitRow()
    {
        var row = new ExpenseSplitRowVM
        {
            SelectedExpenseCategory = SelectedExpenseCategory,
            SelectedTag = SelectedTag
        };

        AddSplitRow(row);
    }

    private void AddSplitRow(ExpenseSplitRowVM row)
    {
        row.PropertyChanged += OnSplitRowPropertyChanged;
        SplitRows.Add(row);
        NotifySplitRowStateChanged();
        RecalculateSplitRemainder(row);
    }

    public void RemoveSplitRow(ExpenseSplitRowVM row)
    {
        row.PropertyChanged -= OnSplitRowPropertyChanged;
        if (row.ExpenseLogId is not null)
            _removedSplitRows.Add(row);

        SplitRows.Remove(row);
        NotifySplitRowStateChanged();
        RecalculateSplitRemainder(SplitRows.LastOrDefault());
    }

    public async Task LoadChildTransactionsAsync(CancellationToken cancellationToken = default)
    {
        var childTransactions = (await LoadChildExpenseLogsAsync(cancellationToken))
            .Select(ProjectChildTransaction)
            .ToList();

        ChildTransactions.Clear();
        foreach (var childTransaction in childTransactions)
            ChildTransactions.Add(childTransaction);

        OnPropertyChanged(nameof(HasChildTransactions));
        OnPropertyChanged(nameof(ShowChildTransactions));
        OnPropertyChanged(nameof(DetailPopupWidth));
    }

    private async Task LoadChildTransactionsIntoSplitRowsAsync(CancellationToken cancellationToken)
    {
        var childLogs = await LoadChildExpenseLogsAsync(cancellationToken);
        if (childLogs.Count == 0)
            return;

        foreach (var row in SplitRows)
            row.PropertyChanged -= OnSplitRowPropertyChanged;

        SplitRows.Clear();
        _removedSplitRows.Clear();

        foreach (var childLog in childLogs)
            AddSplitRow(ProjectSplitRow(childLog));
    }

    private async Task<IReadOnlyList<ExpenseLog>> LoadChildExpenseLogsAsync(CancellationToken cancellationToken)
    {
        return (await _appData.GetExpenseLogsAsync(cancellationToken))
            .Where(log => !log.IsForDeletion)
            .Where(log => log.ParentLogId == _expenseLog.Id)
            .OrderByDescending(log => log.DeductedOn)
            .ThenBy(log => log.Expense?.Name ?? "Expense", StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void ClearSplitMode()
    {
        foreach (var row in SplitRows)
            row.PropertyChanged -= OnSplitRowPropertyChanged;

        SplitRows.Clear();
        _removedSplitRows.Clear();
        IsSplitMode = false;
        HasNegativeSplitRemainder = false;
        NegativeRemainderRow = null;
        NotifySplitRowStateChanged();
    }

    public void RecalculateSplitRemainder(ExpenseSplitRowVM? changedRow)
    {
        var remainder = _savedState.Amount - SplitRows.Sum(row => row.AmountText);
        _isApplyingSplitRemainder = true;
        try
        {
            AmountText = remainder;
        }
        finally
        {
            _isApplyingSplitRemainder = false;
        }

        UpdateSplitNegativeRemainderState(remainder, changedRow);
    }

    public async Task EnsureTagsLoadedAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ExpenseTag> allTags;
        try
        {
            allTags = await _appData.GetExpenseTagsAsync(cancellationToken);
        }
        catch
        {
            return;
        }

        var selectedTagId = SelectedTag?.Id;
        var persistedTags = AddNewTransactionVM.ProjectNonSystemTags(allTags).ToList();
        if (persistedTags.Count == 0)
            return;

        _orderedTags.Clear();
        _orderedTags.AddRange(persistedTags);
        RefreshTagCollections();

        SelectedTag = selectedTagId is null
            ? _orderedTags.FirstOrDefault()
            : _orderedTags.FirstOrDefault(tag => tag.Id == selectedTagId.Value) ?? _orderedTags.FirstOrDefault();
    }

    public AddNewTransactionVM.AddNewTransactionDraft CreateAddNewTransactionDraft()
    {
        return new AddNewTransactionVM.AddNewTransactionDraft(
            true,
            NameText,
            AmountText,
            SelectedAccount?.Id,
            SelectedDate.Date,
            NoteText,
            SelectedExpenseCategory,
            SelectedTag?.Id);
    }

    public async Task<ExpenseDetailSaveResult> SaveAsync(bool keepParentExpenseWhenRemainder = false)
    {
        if (IsSaving)
            return ExpenseDetailSaveResult.Failure("This expense is already being saved.");

        if (IsSplitMode)
            return await SaveSplitAsync(keepParentExpenseWhenRemainder);

        if (!TryBuildInput(out var input, out var validationMessage))
            return ExpenseDetailSaveResult.Failure(validationMessage);

        var previousState = CreateMessageSnapshot(_savedState);
        var changedFields = GetChangedFields(input, _savedState);
        if (changedFields == ExpenseDetailChangedFields.None)
        {
            IsEditing = false;
            ClearSplitMode();
            LoadFromSavedState();
            return ExpenseDetailSaveResult.Success();
        }

        IsSaving = true;

        try
        {
            var expenseLog = await _appData.GetExpenseLogByLogIdAsync(_expenseLog.Id);
            if (expenseLog?.Expense is null)
                return ExpenseDetailSaveResult.Failure("Unable to load this expense.");

            var beforeHistorySnapshot = ExpenseLogMemorySnapshot.Create(expenseLog);

            var expense = expenseLog.Expense;
            var currentAccount = expenseLog.Account;
            if (currentAccount is null)
                return ExpenseDetailSaveResult.Failure("Unable to load this expense source.");

            var newAccount = await _appData.GetAccountByIdAsync(input.AccountId);
            if (newAccount is null)
                return ExpenseDetailSaveResult.Failure("Please select a valid account.");

            var expenseTag = await _appData.GetExpenseTagByIdAsync(input.TagId);
            if (expenseTag is null)
                return ExpenseDetailSaveResult.Failure("Please select a valid tag.");

            var resolvedName = BuildExpenseName(input.Name, input.Note, expenseTag.Name);

            var sourceChanged = currentAccount.Id != newAccount.Id;
            if (!sourceChanged)
            {
                RevertExpenseFromAccount(currentAccount, expenseLog.Amount);
                ApplyExpenseToAccount(currentAccount, input.Amount);
                newAccount = currentAccount;
            }
            else
            {
                RevertExpenseFromAccount(currentAccount, expenseLog.Amount);
                ApplyExpenseToAccount(newAccount, input.Amount);
            }

            expense.Name = resolvedName;
            expense.Amount = input.Amount;
            expense.ExpenseCategory = input.Category;
            expense.Account = newAccount;
            expense.ExpenseTag = expenseTag;

            expenseLog.Amount = input.Amount;
            expenseLog.IsPinned = input.IsPinned;
            expenseLog.DeductedOn = input.Date;
            expenseLog.Notes = input.Note;
            expenseLog.Account = newAccount;

            if (sourceChanged)
                await CascadeAccountToChildExpenseLogsAsync(newAccount);

            _appData.UpdateExpense(expense);
            _appData.UpdateExpenseLog(expenseLog);
            _appData.UpdateAccount(currentAccount);

            if (sourceChanged)
                _appData.UpdateAccount(newAccount);

            await _appData.SaveChangesAsync();
            _savedState = new ExpenseDetailSavedState(
                resolvedName,
                input.Amount,
                input.IsPinned,
                input.Note,
                input.Date,
                input.Category,
                input.AccountId,
                input.TagId);

            IsEditing = false;
            ClearSplitMode();
            LoadFromSavedState();
            WeakReferenceMessenger.Default.Send(new ExpenseDetailUpdatedMessage(
                new ExpenseDetailUpdate(_expenseLog.Id, previousState, changedFields)));
            WeakReferenceMessenger.Default.Send(new RecordLogMemoryMessage(
                new EditExpenseLogMemoryAction(beforeHistorySnapshot, ExpenseLogMemorySnapshot.Create(expenseLog))));
            return ExpenseDetailSaveResult.Success();
        }
        catch (Exception exception)
        {
            FluxoLogManager.LogError(exception, "Unable to save expense detail changes.");
            return ExpenseDetailSaveResult.Failure(FluxoLogManager.CreateFailureMessage("save expense"));
        }
        finally
        {
            IsSaving = false;
        }
    }

    public bool HasValidChangesToPersistOnClose()
    {
        if (!IsEditing)
            return false;

        if (IsSplitMode)
            return HasSplitRowsWithAmounts;

        if (!TryBuildInput(out var input, out _))
            return false;

        return GetChangedFields(input, _savedState) != ExpenseDetailChangedFields.None;
    }

    public async Task<ExpenseDetailSaveResult> DeleteAsync()
    {
        if (IsSaving)
            return ExpenseDetailSaveResult.Failure("This expense is already being saved.");

        IsSaving = true;
        try
        {
            var expenseLog = await _appData.GetExpenseLogByLogIdAsync(_expenseLog.Id);
            if (expenseLog is null)
                return ExpenseDetailSaveResult.Failure("Unable to load this expense.");

            var snapshot = ExpenseLogMemorySnapshot.Create(expenseLog);
            if (expenseLog.Account is { } account)
            {
                RevertExpenseFromAccount(account, expenseLog.Amount);
                _appData.UpdateAccount(account);
            }

            _appData.RemoveExpenseLog(expenseLog);
            await _appData.SaveChangesAsync();

            WeakReferenceMessenger.Default.Send(new RecordLogMemoryMessage(new DeleteExpenseLogMemoryAction(snapshot)));
            WeakReferenceMessenger.Default.Send(new DashboardDataInvalidatedMessage(DashboardDataInvalidationScope.Budget));
            return ExpenseDetailSaveResult.Success();
        }
        catch (Exception exception)
        {
            FluxoLogManager.LogError(exception, "Unable to delete expense detail.");
            return ExpenseDetailSaveResult.Failure(FluxoLogManager.CreateFailureMessage("delete expense"));
        }
        finally
        {
            IsSaving = false;
        }
    }

    public void SetVisibleTagSlots(int visibleTagSlots)
    {
        var normalizedSlots = Math.Max(0, visibleTagSlots);
        if (_visibleTagSlots == normalizedSlots)
            return;

        _visibleTagSlots = normalizedSlots;
        RefreshTagCollections();
    }

    private void LoadFromSavedState()
    {
        AmountText = _savedState.Amount;
        NameText = _savedState.Name;
        IsPinned = _savedState.IsPinned;
        NoteText = _savedState.Note;
        SelectedDate = _savedState.Date == default ? DateTime.Today : _savedState.Date.Date;
        SelectedExpenseCategory = _savedState.Category;
        SelectedAccount = Accounts.FirstOrDefault(source => source.Id == _savedState.AccountId) ??
                                 Accounts.FirstOrDefault();
        SelectedTag = _orderedTags.FirstOrDefault(tag => tag.Id == _savedState.TagId) ??
                      _orderedTags.FirstOrDefault();
        PopupTitle = string.IsNullOrWhiteSpace(NameText) ? "Expense Detail" : NameText.Trim();
        IsMoreTagsOpen = false;
    }

    private bool TryBuildInput(out ExpenseDetailInput input, out string validationMessage)
    {
        input = default;
        validationMessage = string.Empty;

        if (AmountText <= 0m)
        {
            validationMessage = "Please enter a valid amount greater than zero.";
            return false;
        }

        if (SelectedAccount is null)
        {
            validationMessage = "Please choose a account.";
            return false;
        }

        if (SelectedTag is null)
        {
            validationMessage = "Please choose a tag.";
            return false;
        }

        input = new ExpenseDetailInput(
            NameText.Trim(),
            AmountText,
            IsPinned,
            SelectedAccount.Id,
            SelectedDate.Date,
            NoteText.Trim(),
            SelectedExpenseCategory,
            SelectedTag.Id);

        return true;
    }

    private bool TryBuildSplitInputs(
        bool keepParentExpenseWhenRemainder,
        out IReadOnlyList<ExpenseSplitInput> inputs,
        out string validationMessage)
    {
        inputs = [];
        validationMessage = string.Empty;

        if (HasNegativeSplitRemainder || AmountText < 0m)
        {
            validationMessage = "Split amounts exceed the original expense amount.";
            return false;
        }

        if (SplitRows.Any(row => row.AmountText < 0m))
        {
            validationMessage = "Split amounts cannot be negative.";
            return false;
        }

        _ = keepParentExpenseWhenRemainder;
        var result = new List<ExpenseSplitInput>();

        foreach (var row in SplitRows.Where(row => row.AmountText > 0m))
        {
            if (row.SelectedTag is null)
            {
                validationMessage = "Please choose a tag for each split row.";
                return false;
            }

            result.Add(new ExpenseSplitInput(
                row.ExpenseLogId,
                _expenseLog.Id,
                row.NameText.Trim(),
                row.AmountText,
                row.SelectedExpenseCategory,
                row.SelectedTag.Id,
                string.Empty,
                SelectedDate.Date));
        }

        if (result.Count == 0 && _removedSplitRows.All(row => row.ExpenseLogId is null))
        {
            validationMessage = "Add at least one split amount before saving.";
            return false;
        }

        var splitRowsPositiveTotal = SplitRows
            .Where(row => row.AmountText > 0m)
            .Sum(row => row.AmountText);
        if (splitRowsPositiveTotal > _savedState.Amount)
        {
            validationMessage = "Split amounts exceed the original expense amount.";
            return false;
        }

        inputs = result;
        return true;
    }

    private async Task<ExpenseDetailSaveResult> SaveSplitAsync(bool keepParentExpenseWhenRemainder)
    {
        if (!TryBuildSplitInputs(keepParentExpenseWhenRemainder, out var inputs, out var validationMessage))
            return ExpenseDetailSaveResult.Failure(validationMessage);

        IsSaving = true;

        try
        {
            var originalLog = await _appData.GetExpenseLogByLogIdAsync(_expenseLog.Id);
            if (originalLog?.Expense is null)
                return ExpenseDetailSaveResult.Failure("Unable to load this expense.");

            var account = originalLog.Account;
            if (account is null)
                return ExpenseDetailSaveResult.Failure("Unable to load this expense source.");

            var splitEntries = new List<(ExpenseSplitInput Input, ExpenseTag Tag)>();
            foreach (var input in inputs)
            {
                var tag = await _appData.GetExpenseTagByIdAsync(input.TagId);
                if (tag is null)
                    return ExpenseDetailSaveResult.Failure("Please select a valid tag.");

                splitEntries.Add((input, tag));
            }

            _ = keepParentExpenseWhenRemainder;
            originalLog.ParentLogId = null;
            var existingChildren = (await LoadChildExpenseLogsAsync(CancellationToken.None))
                .ToDictionary(log => log.Id);

            var changedSnapshots = new List<(ExpenseLogMemorySnapshot Before, ExpenseLogMemorySnapshot After)>();
            var createdLogs = new List<(Expense Expense, ExpenseLog ExpenseLog, ExpenseTag Tag)>(splitEntries.Count);
            foreach (var (input, tag) in splitEntries)
            {
                if (input.ExpenseLogId is { } childLogId &&
                    existingChildren.TryGetValue(childLogId, out var existingChild) &&
                    existingChild.Expense is not null)
                {
                    var beforeSnapshot = ExpenseLogMemorySnapshot.Create(existingChild);
                    ApplySplitInputToExistingChild(existingChild, input, tag, account);
                    _appData.UpdateExpense(existingChild.Expense);
                    _appData.UpdateExpenseLog(existingChild);
                    changedSnapshots.Add((beforeSnapshot, ExpenseLogMemorySnapshot.Create(existingChild)));
                    continue;
                }

                var expense = CreateSplitExpense(input, tag, account);
                var expenseLog = CreateSplitExpenseLog(input, expense, account, originalLog.Id);

                await _appData.AddExpenseAsync(expense);
                await _appData.AddExpenseLogAsync(expenseLog);
                createdLogs.Add((expense, expenseLog, tag));
            }

            var retainedChildIds = inputs
                .Select(input => input.ExpenseLogId)
                .OfType<int>()
                .ToHashSet();
            var removedChildIds = new HashSet<int>();

            foreach (var removedRow in _removedSplitRows.Where(row => row.ExpenseLogId is not null))
            {
                var removedChildId = removedRow.ExpenseLogId!.Value;
                removedChildIds.Add(removedChildId);

                if (!existingChildren.TryGetValue(removedChildId, out var removedChild) ||
                    removedChild.Expense is null ||
                    removedChild.IsForDeletion)
                    continue;

                if (removedRow.SelectedTag is null)
                {
                    removedChild.IsForDeletion = true;
                    _appData.UpdateExpenseLog(removedChild);
                    continue;
                }

                var removedTag = await _appData.GetExpenseTagByIdAsync(removedRow.SelectedTag.Id);
                if (removedTag is null)
                    return ExpenseDetailSaveResult.Failure("Please select a valid tag.");

                var input = new ExpenseSplitInput(
                    removedRow.ExpenseLogId,
                    originalLog.Id,
                    removedRow.NameText.Trim(),
                    removedRow.AmountText,
                    removedRow.SelectedExpenseCategory,
                    removedRow.SelectedTag.Id,
                    string.Empty,
                    SelectedDate.Date);
                var beforeSnapshot = ExpenseLogMemorySnapshot.Create(removedChild);
                ApplySplitInputToExistingChild(removedChild, input, removedTag, account);
                removedChild.IsForDeletion = true;
                _appData.UpdateExpense(removedChild.Expense);
                _appData.UpdateExpenseLog(removedChild);
                changedSnapshots.Add((beforeSnapshot, ExpenseLogMemorySnapshot.Create(removedChild)));
            }

            foreach (var staleChild in existingChildren.Values.Where(child =>
                         !retainedChildIds.Contains(child.Id) &&
                         !removedChildIds.Contains(child.Id)))
            {
                var beforeSnapshot = ExpenseLogMemorySnapshot.Create(staleChild);
                staleChild.IsForDeletion = true;
                _appData.UpdateExpenseLog(staleChild);
                changedSnapshots.Add((beforeSnapshot, ExpenseLogMemorySnapshot.Create(staleChild)));
            }

            await _appData.SaveChangesAsync();

            var createdSnapshots = createdLogs.Select(entry => new ExpenseLogMemorySnapshot(
                entry.Expense.Id,
                entry.ExpenseLog.Id,
                entry.Expense.Name,
                entry.ExpenseLog.Amount,
                entry.Expense.ExpenseCategory,
                account.Id,
                entry.Tag.Id,
                entry.ExpenseLog.DeductedOn,
                entry.ExpenseLog.Notes,
                entry.ExpenseLog.IsForDeletion,
                entry.ExpenseLog.ParentLogId)).ToList();

            var historyActions = changedSnapshots
                .Select(snapshots => (ILogMemoryAction)new EditExpenseLogMemoryAction(snapshots.Before, snapshots.After))
                .Concat(createdSnapshots
                .Select(snapshot => (ILogMemoryAction)new AddExpenseLogMemoryAction(snapshot, shouldAdjustAccountTotals: false))
                )
                .ToList();

            WeakReferenceMessenger.Default.Send(new RecordLogMemoryMessage(
                new CompositeLogMemoryAction("Split expense", historyActions)));
            WeakReferenceMessenger.Default.Send(new DashboardDataInvalidatedMessage(
                DashboardDataInvalidationScope.Budget | DashboardDataInvalidationScope.Notifications));
            await _mainViewModel.ReloadCurrentDataAsync();
            await LoadChildTransactionsAsync();

            IsEditing = false;
            ClearSplitMode();
            LoadFromSavedState();
            return ExpenseDetailSaveResult.Success();
        }
        catch (Exception exception)
        {
            FluxoLogManager.LogError(exception, "Unable to split expense.");
            return ExpenseDetailSaveResult.Failure(FluxoLogManager.CreateFailureMessage("split expense"));
        }
        finally
        {
            IsSaving = false;
        }
    }

    private void ReloadChoicesFromMainViewModel()
    {
        _availableAccounts.Clear();
        _availableAccounts.AddRange(_mainViewModel.BudgetPanel.Accounts.Where(source => source.IsEnabled));

        _orderedTags.Clear();
        _orderedTags.AddRange(_mainViewModel.BudgetPanel.Tags
            .Concat(_mainViewModel.BudgetPanel.OtherTags)
            .GroupBy(tag => tag.Id)
            .Select(group => group.First()));

        RefreshTagCollections();
        RefreshAccounts();
    }

    private void PromoteTagToVisibleStart(ExpenseTagVM selectedTag)
    {
        var reorderedTags = _orderedTags
            .Where(tag => tag.Id != selectedTag.Id)
            .Prepend(selectedTag)
            .ToList();

        _orderedTags.Clear();
        _orderedTags.AddRange(reorderedTags);

        RefreshTagCollections();
        SelectedTag = _orderedTags.FirstOrDefault(tag => tag.Id == selectedTag.Id);
    }

    private void RefreshTagCollections()
    {
        var selectedTagId = SelectedTag?.Id;

        _isUpdatingTagCollections = true;

        try
        {
            if (!IsEditing)
            {
                ReplaceCollection(VisibleTags, SelectedTag is null ? [] : [SelectedTag]);
                ReplaceCollection(OverflowTags, []);
                OnPropertyChanged(nameof(HasMoreTags));
                OnPropertyChanged(nameof(AllSplitTags));
                IsMoreTagsOpen = false;
                return;
            }

            var editableTags = _orderedTags.Where(tag => !tag.IsSystemTag).ToList();
            ReplaceCollection(VisibleTags, editableTags.Take(_visibleTagSlots));
            ReplaceCollection(OverflowTags, editableTags.Skip(_visibleTagSlots));

            OnPropertyChanged(nameof(HasMoreTags));
            OnPropertyChanged(nameof(AllSplitTags));
            if (!HasMoreTags)
                IsMoreTagsOpen = false;

            if (selectedTagId is null)
                return;

            SelectedTag = _orderedTags.FirstOrDefault(tag => tag.Id == selectedTagId.Value);
        }
        finally
        {
            _isUpdatingTagCollections = false;
        }
    }

    private void RefreshAccounts()
    {
        var selectedAccountId = SelectedAccount?.Id;
        ReplaceCollection(Accounts, _availableAccounts
            .OrderBy(source => source.AccountType)
            .ThenBy(source => source.Name));

        SelectedAccount = selectedAccountId is null
            ? Accounts.FirstOrDefault()
            : Accounts.FirstOrDefault(source => source.Id == selectedAccountId.Value) ??
              Accounts.FirstOrDefault();
    }

    private static string BuildExpenseName(string name, string note, string fallbackName)
    {
        if (!string.IsNullOrWhiteSpace(name))
            return name.Trim();

        if (string.IsNullOrWhiteSpace(note))
            return fallbackName;

        var firstMeaningfulLine = note
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));

        return string.IsNullOrWhiteSpace(firstMeaningfulLine)
            ? fallbackName
            : firstMeaningfulLine;
    }

    private static ExpenseDetailChildTransactionVM ProjectChildTransaction(ExpenseLog log)
    {
        return new ExpenseDetailChildTransactionVM
        {
            Id = log.Id,
            Name = log.Expense?.Name ?? "Expense",
            Amount = log.Amount,
            DeductedOn = log.DeductedOn,
            Category = log.Expense?.ExpenseCategory ?? ExpenseCategory.Needs,
            AccountName = log.Account?.Name ?? string.Empty,
            TagName = log.Expense?.ExpenseTag?.Name ?? string.Empty,
            TagHexCode = log.Expense?.ExpenseTag?.HexCode ?? string.Empty,
            Notes = log.Notes
        };
    }

    private ExpenseSplitRowVM ProjectSplitRow(ExpenseLog log)
    {
        return new ExpenseSplitRowVM
        {
            ExpenseLogId = log.Id,
            AmountText = log.Amount,
            NameText = log.Expense?.Name ?? string.Empty,
            SelectedExpenseCategory = log.Expense?.ExpenseCategory ?? SelectedExpenseCategory,
            SelectedTag = ResolveSplitRowTag(log.Expense?.ExpenseTag)
        };
    }

    private ExpenseTagVM? ResolveSplitRowTag(ExpenseTag? tag)
    {
        if (tag is null)
            return SelectedTag;

        var existingTag = _orderedTags.FirstOrDefault(candidate => candidate.Id == tag.Id);
        if (existingTag is not null)
            return existingTag;

        return new ExpenseTagVM
        {
            Id = tag.Id,
            Name = tag.Name,
            HexCode = tag.HexCode,
            IsSystemTag = tag.IsSystemTag
        };
    }

    private async Task CascadeAccountToChildExpenseLogsAsync(Account account, CancellationToken cancellationToken = default)
    {
        var childLogs = await LoadChildExpenseLogsAsync(cancellationToken);
        foreach (var childLog in childLogs)
        {
            if (childLog.Expense is not null)
            {
                childLog.Expense.Account = account;
                childLog.Expense.AccountId = account.Id;
                _appData.UpdateExpense(childLog.Expense);
            }

            childLog.Account = account;
            childLog.AccountId = account.Id;
            _appData.UpdateExpenseLog(childLog);
        }
    }

    private static Expense CreateSplitExpense(ExpenseSplitInput input, ExpenseTag tag, Account account)
    {
        return new Expense
        {
            Name = BuildExpenseName(input.Name, input.Note, tag.Name),
            Amount = input.Amount,
            ExpenseCategory = input.Category,
            AccountId = account.Id,
            ExpenseTagId = tag.Id
        };
    }

    private static ExpenseLog CreateSplitExpenseLog(
        ExpenseSplitInput input,
        Expense expense,
        Account account,
        int parentLogId)
    {
        return new ExpenseLog
        {
            Expense = expense,
            Amount = input.Amount,
            DeductedOn = input.Date,
            Notes = input.Note,
            IsForDeletion = false,
            AccountId = account.Id,
            ParentLogId = parentLogId
        };
    }

    private static void ApplySplitInputToExistingChild(
        ExpenseLog expenseLog,
        ExpenseSplitInput input,
        ExpenseTag tag,
        Account account)
    {
        expenseLog.Expense!.Name = BuildExpenseName(input.Name, input.Note, tag.Name);
        expenseLog.Expense.Amount = input.Amount;
        expenseLog.Expense.ExpenseCategory = input.Category;
        expenseLog.Expense.Account = account;
        expenseLog.Expense.AccountId = account.Id;
        expenseLog.Expense.ExpenseTag = tag;
        expenseLog.Expense.ExpenseTagId = tag.Id;

        expenseLog.Amount = input.Amount;
        expenseLog.DeductedOn = input.Date;
        expenseLog.Notes = input.Note;
        expenseLog.Account = account;
        expenseLog.AccountId = account.Id;
        expenseLog.IsForDeletion = false;
        expenseLog.ParentLogId = input.ParentLogId;
    }

    private static void ApplyExpenseToAccount(Account account, decimal amount)
    {
        if (account.AccountType == AccountType.Credit)
        {
            account.SpentAmount += amount;
            return;
        }

        account.Balance -= amount;
    }

    private static bool IsBudgetReconciliationExpenseLog(ExpenseLog expenseLog)
    {
        var tag = expenseLog.Expense?.ExpenseTag;
        return tag is { IsSystemTag: true } &&
               string.Equals(tag.Name, SystemExpenseTags.BudgetReconciliationName, StringComparison.OrdinalIgnoreCase);
    }

    private static void RevertExpenseFromAccount(Account account, decimal amount)
    {
        if (account.AccountType == AccountType.Credit)
        {
            account.SpentAmount = Math.Max(0m, account.SpentAmount - amount);
            return;
        }

        account.Balance += amount;
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();

        foreach (var item in items)
            target.Add(item);
    }

    private static ExpenseDetailSavedState CreateSavedState(ExpenseLogVM expenseLog)
    {
        return new ExpenseDetailSavedState(
            expenseLog.Expense?.Name?.Trim() ?? string.Empty,
            expenseLog.Amount,
            expenseLog.IsPinned,
            expenseLog.Notes?.Trim() ?? string.Empty,
            expenseLog.DeductedOn == default ? DateTime.Today : expenseLog.DeductedOn.Date,
            expenseLog.Expense?.ExpenseCategory ?? ExpenseCategory.Needs,
            expenseLog.Account?.Id ?? 0,
            expenseLog.Expense?.ExpenseTag?.Id ?? 0);
    }

    private static ExpenseDetailSnapshot CreateMessageSnapshot(ExpenseDetailSavedState savedState)
    {
        return new ExpenseDetailSnapshot(
            savedState.Amount,
            savedState.Date,
            savedState.Category,
            savedState.AccountId,
            savedState.TagId);
    }

    private static ExpenseDetailChangedFields GetChangedFields(ExpenseDetailInput input,
        ExpenseDetailSavedState savedState)
    {
        var changedFields = ExpenseDetailChangedFields.None;

        if (!string.Equals(input.Name, savedState.Name, StringComparison.Ordinal))
            changedFields |= ExpenseDetailChangedFields.Name;

        if (input.Amount != savedState.Amount)
            changedFields |= ExpenseDetailChangedFields.Amount;

        if (input.IsPinned != savedState.IsPinned)
            changedFields |= ExpenseDetailChangedFields.Pin;

        if (input.Date.Date != savedState.Date.Date)
            changedFields |= ExpenseDetailChangedFields.Date;

        if (input.Category != savedState.Category)
            changedFields |= ExpenseDetailChangedFields.Category;

        if (input.AccountId != savedState.AccountId)
            changedFields |= ExpenseDetailChangedFields.Account;

        if (input.TagId != savedState.TagId)
            changedFields |= ExpenseDetailChangedFields.Tag;

        if (!string.Equals(input.Note, savedState.Note, StringComparison.Ordinal))
            changedFields |= ExpenseDetailChangedFields.Note;

        return changedFields;
    }

    private void NotifySplitRowStateChanged()
    {
        OnPropertyChanged(nameof(HasSplitRows));
        OnPropertyChanged(nameof(HasSplitRowsWithAmounts));
        OnPropertyChanged(nameof(HasSplitRowsWithoutAmounts));
        OnPropertyChanged(nameof(CanCloseSplitModeWithoutSaving));
        OnPropertyChanged(nameof(RequiresEmptySplitConfirmationOnClose));
    }

    private void OnSplitRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ExpenseSplitRowVM row)
            return;

        if (e.PropertyName != nameof(ExpenseSplitRowVM.AmountText))
            return;

        NotifySplitRowStateChanged();
        RecalculateSplitRemainder(row);
    }

    private void UpdateSplitNegativeRemainderState(decimal remainder, ExpenseSplitRowVM? causingRow)
    {
        foreach (var row in SplitRows)
            row.IsCausingNegativeRemainder = false;

        HasNegativeSplitRemainder = remainder < 0m;
        NegativeRemainderRow = HasNegativeSplitRemainder ? causingRow : null;

        if (NegativeRemainderRow is not null)
            NegativeRemainderRow.IsCausingNegativeRemainder = true;
    }

    public sealed record ExpenseCategoryOption(string Label, ExpenseCategory Value);

    public readonly record struct ExpenseDetailSaveResult(bool IsSuccess, string? ErrorMessage)
    {
        public static ExpenseDetailSaveResult Success()
        {
            return new ExpenseDetailSaveResult(true, null);
        }

        public static ExpenseDetailSaveResult Failure(string? errorMessage)
        {
            return new ExpenseDetailSaveResult(false, errorMessage);
        }
    }

    private readonly record struct ExpenseDetailInput(
        string Name,
        decimal Amount,
        bool IsPinned,
        int AccountId,
        DateTime Date,
        string Note,
        ExpenseCategory Category,
        int TagId);

    private readonly record struct ExpenseSplitInput(
        int? ExpenseLogId,
        int ParentLogId,
        string Name,
        decimal Amount,
        ExpenseCategory Category,
        int TagId,
        string Note,
        DateTime Date);

    private readonly record struct ExpenseDetailSavedState(
        string Name,
        decimal Amount,
        bool IsPinned,
        string Note,
        DateTime Date,
        ExpenseCategory Category,
        int AccountId,
        int TagId);
}
