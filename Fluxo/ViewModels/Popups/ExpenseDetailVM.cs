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
    private readonly List<SpendingSourceVM> _availableSpendingSources = [];
    private readonly ExpenseLogVM _expenseLog;
    private readonly MainVM _mainViewModel;
    private readonly List<ExpenseTagVM> _orderedTags = [];
    private readonly IAppDataService _appData;

    [ObservableProperty] private decimal _amountText;
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

    private ExpenseDetailSavedState _savedState = new(string.Empty, 0m, string.Empty, DateTime.Today,
        ExpenseCategory.Needs, 0, 0);

    [ObservableProperty] private DateTime _selectedDate = DateTime.Today;
    [ObservableProperty] private ExpenseCategory _selectedExpenseCategory = ExpenseCategory.Needs;
    [ObservableProperty] private SpendingSourceVM? _selectedSpendingSource;
    [ObservableProperty] private ExpenseTagVM? _selectedTag;

    public ExpenseDetailVM(MainVM mainViewModel, ExpenseLogVM expenseLog, IAppDataService appData)
    {
        _mainViewModel = mainViewModel;
        _expenseLog = expenseLog;
        _appData = appData;
        SpendingSourcesView = SpendingSourceComboBoxViewFactory.CreateGroupedByTypeThenName(
            SpendingSources,
            nameof(SpendingSourceVM.TypeDisplayName),
            nameof(SpendingSourceVM.SpendingSourceType),
            nameof(SpendingSourceVM.Name));

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

    public ObservableCollection<SpendingSourceVM> SpendingSources { get; } = [];
    public ICollectionView SpendingSourcesView { get; }
    public ObservableCollection<ExpenseSplitRowVM> SplitRows { get; } = [];
    public ObservableCollection<ExpenseTagVM> VisibleTags { get; } = [];
    public ObservableCollection<ExpenseTagVM> OverflowTags { get; } = [];

    public bool AreFieldsReadOnly => !IsEditing;
    public bool CanEditFields => IsEditing;
    public bool HasMoreTags => OverflowTags.Count > 0;
    public bool HasSplitRows => SplitRows.Count > 0;
    public bool HasSplitRowsWithAmounts => SplitRows.Any(row => row.HasAmount);
    public bool HasSplitRowsWithoutAmounts => SplitRows.Count > 0 && SplitRows.All(row => !row.HasAmount);
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

    public void AddSplitRow()
    {
        var row = new ExpenseSplitRowVM
        {
            SelectedExpenseCategory = SelectedExpenseCategory,
            SelectedTag = SelectedTag
        };

        row.PropertyChanged += OnSplitRowPropertyChanged;
        SplitRows.Add(row);
        NotifySplitRowStateChanged();
        RecalculateSplitRemainder(row);
    }

    public void RemoveSplitRow(ExpenseSplitRowVM row)
    {
        row.PropertyChanged -= OnSplitRowPropertyChanged;
        SplitRows.Remove(row);
        NotifySplitRowStateChanged();
        RecalculateSplitRemainder(SplitRows.LastOrDefault());
    }

    public void ClearSplitMode()
    {
        foreach (var row in SplitRows)
            row.PropertyChanged -= OnSplitRowPropertyChanged;

        SplitRows.Clear();
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
            SelectedSpendingSource?.Id,
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
            var currentSpendingSource = expenseLog.SpendingSource;
            if (currentSpendingSource is null)
                return ExpenseDetailSaveResult.Failure("Unable to load this expense source.");

            var newSpendingSource = await _appData.GetSpendingSourceByIdAsync(input.SpendingSourceId);
            if (newSpendingSource is null)
                return ExpenseDetailSaveResult.Failure("Please select a valid spending source.");

            var expenseTag = await _appData.GetExpenseTagByIdAsync(input.TagId);
            if (expenseTag is null)
                return ExpenseDetailSaveResult.Failure("Please select a valid tag.");

            var resolvedName = BuildExpenseName(input.Name, input.Note, expenseTag.Name);

            var sourceChanged = currentSpendingSource.Id != newSpendingSource.Id;
            if (!sourceChanged)
            {
                RevertExpenseFromSpendingSource(currentSpendingSource, expenseLog.Amount);
                ApplyExpenseToSpendingSource(currentSpendingSource, input.Amount);
                newSpendingSource = currentSpendingSource;
            }
            else
            {
                RevertExpenseFromSpendingSource(currentSpendingSource, expenseLog.Amount);
                ApplyExpenseToSpendingSource(newSpendingSource, input.Amount);
            }

            expense.Name = resolvedName;
            expense.Amount = input.Amount;
            expense.ExpenseCategory = input.Category;
            expense.SpendingSource = newSpendingSource;
            expense.ExpenseTag = expenseTag;

            expenseLog.Amount = input.Amount;
            expenseLog.DeductedOn = input.Date;
            expenseLog.Notes = input.Note;
            expenseLog.SpendingSource = newSpendingSource;

            _appData.UpdateExpense(expense);
            _appData.UpdateExpenseLog(expenseLog);
            _appData.UpdateSpendingSource(currentSpendingSource);

            if (sourceChanged)
                _appData.UpdateSpendingSource(newSpendingSource);

            await _appData.SaveChangesAsync();
            _savedState = new ExpenseDetailSavedState(
                resolvedName,
                input.Amount,
                input.Note,
                input.Date,
                input.Category,
                input.SpendingSourceId,
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
        NoteText = _savedState.Note;
        SelectedDate = _savedState.Date == default ? DateTime.Today : _savedState.Date.Date;
        SelectedExpenseCategory = _savedState.Category;
        SelectedSpendingSource = SpendingSources.FirstOrDefault(source => source.Id == _savedState.SpendingSourceId) ??
                                 SpendingSources.FirstOrDefault();
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

        if (SelectedSpendingSource is null)
        {
            validationMessage = "Please choose a spending source.";
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
            SelectedSpendingSource.Id,
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

        var result = new List<ExpenseSplitInput>();
        var shouldCreateParentReplacement = !keepParentExpenseWhenRemainder && AmountText > 0m;
        if (shouldCreateParentReplacement)
        {
            if (SelectedTag is null)
            {
                validationMessage = "Please choose a tag.";
                return false;
            }

            result.Add(new ExpenseSplitInput(
                NameText.Trim(),
                AmountText,
                SelectedExpenseCategory,
                SelectedTag.Id,
                NoteText.Trim(),
                SelectedDate.Date));
        }

        foreach (var row in SplitRows.Where(row => row.AmountText > 0m))
        {
            if (row.SelectedTag is null)
            {
                validationMessage = "Please choose a tag for each split row.";
                return false;
            }

            result.Add(new ExpenseSplitInput(
                row.NameText.Trim(),
                row.AmountText,
                row.SelectedExpenseCategory,
                row.SelectedTag.Id,
                string.Empty,
                SelectedDate.Date));
        }

        if (result.Count == 0)
        {
            validationMessage = "Add at least one split amount before saving.";
            return false;
        }

        var splitRowsPositiveTotal = SplitRows
            .Where(row => row.AmountText > 0m)
            .Sum(row => row.AmountText);
        var splitTotal = AmountText + splitRowsPositiveTotal;
        if (splitTotal != _savedState.Amount)
        {
            validationMessage = "Split total must match the original expense amount.";
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

            var spendingSource = originalLog.SpendingSource;
            if (spendingSource is null)
                return ExpenseDetailSaveResult.Failure("Unable to load this expense source.");

            var splitEntries = new List<(ExpenseSplitInput Input, ExpenseTag Tag)>();
            foreach (var input in inputs)
            {
                var tag = await _appData.GetExpenseTagByIdAsync(input.TagId);
                if (tag is null)
                    return ExpenseDetailSaveResult.Failure("Please select a valid tag.");

                splitEntries.Add((input, tag));
            }

            var deleteSnapshot = ExpenseLogMemorySnapshot.Create(originalLog);

            RevertExpenseFromSpendingSource(spendingSource, originalLog.Amount);
            var keptParentSnapshot = default(ExpenseLogMemorySnapshot?);
            var keepParentWithRemainder = keepParentExpenseWhenRemainder && AmountText > 0m;
            if (keepParentWithRemainder)
            {
                if (SelectedTag is null)
                    return ExpenseDetailSaveResult.Failure("Please choose a tag.");

                var parentTag = await _appData.GetExpenseTagByIdAsync(SelectedTag.Id);
                if (parentTag is null)
                    return ExpenseDetailSaveResult.Failure("Please select a valid tag.");

                var parentExpense = originalLog.Expense;
                var parentName = BuildExpenseName(NameText.Trim(), NoteText.Trim(), parentTag.Name);

                parentExpense.Name = parentName;
                parentExpense.Amount = AmountText;
                parentExpense.ExpenseCategory = SelectedExpenseCategory;
                parentExpense.ExpenseTag = parentTag;
                parentExpense.ExpenseTagId = parentTag.Id;

                originalLog.Amount = AmountText;
                originalLog.DeductedOn = SelectedDate.Date;
                originalLog.Notes = NoteText.Trim();
                originalLog.IsForDeletion = false;

                _appData.UpdateExpense(parentExpense);
                _appData.UpdateExpenseLog(originalLog);
                ApplyExpenseToSpendingSource(spendingSource, AmountText);
                keptParentSnapshot = ExpenseLogMemorySnapshot.Create(originalLog);
            }
            else
            {
                if (IsBudgetReconciliationExpenseLog(originalLog))
                {
                    _appData.RemoveExpenseLog(originalLog);
                    _appData.RemoveExpense(originalLog.Expense);
                }
                else
                {
                    originalLog.IsForDeletion = true;
                    _appData.UpdateExpenseLog(originalLog);
                }
            }

            var createdLogs = new List<(Expense Expense, ExpenseLog ExpenseLog, ExpenseTag Tag)>(splitEntries.Count);
            foreach (var (input, tag) in splitEntries)
            {
                var expense = new Expense
                {
                    Name = BuildExpenseName(input.Name, input.Note, tag.Name),
                    Amount = input.Amount,
                    ExpenseCategory = input.Category,
                    SpendingSourceId = spendingSource.Id,
                    ExpenseTagId = tag.Id
                };
                var expenseLog = new ExpenseLog
                {
                    Expense = expense,
                    Amount = input.Amount,
                    DeductedOn = input.Date,
                    Notes = input.Note,
                    IsForDeletion = false,
                    SpendingSourceId = spendingSource.Id
                };

                await _appData.AddExpenseAsync(expense);
                await _appData.AddExpenseLogAsync(expenseLog);
                ApplyExpenseToSpendingSource(spendingSource, input.Amount);
                createdLogs.Add((expense, expenseLog, tag));
            }

            _appData.UpdateSpendingSource(spendingSource);
            await _appData.SaveChangesAsync();

            var createdSnapshots = createdLogs.Select(entry => new ExpenseLogMemorySnapshot(
                entry.Expense.Id,
                entry.ExpenseLog.Id,
                entry.Expense.Name,
                entry.ExpenseLog.Amount,
                entry.Expense.ExpenseCategory,
                spendingSource.Id,
                entry.Tag.Id,
                entry.ExpenseLog.DeductedOn,
                entry.ExpenseLog.Notes,
                entry.ExpenseLog.IsForDeletion)).ToList();

            var historyActions = new List<ILogMemoryAction>();
            if (keptParentSnapshot is null)
                historyActions.Add(new DeleteExpenseLogMemoryAction(deleteSnapshot));
            else
                historyActions.Add(new EditExpenseLogMemoryAction(deleteSnapshot, keptParentSnapshot));

            historyActions.AddRange(createdSnapshots.Select(snapshot =>
                new AddExpenseLogMemoryAction(snapshot, shouldAdjustSpendingSourceTotals: false)));

            WeakReferenceMessenger.Default.Send(new RecordLogMemoryMessage(
                new CompositeLogMemoryAction("Split expense", historyActions)));
            WeakReferenceMessenger.Default.Send(new DashboardDataInvalidatedMessage(
                DashboardDataInvalidationScope.Budget | DashboardDataInvalidationScope.Notifications));
            await _mainViewModel.ReloadCurrentDataAsync();

            if (keepParentWithRemainder)
            {
                _savedState = new ExpenseDetailSavedState(
                    BuildExpenseName(NameText.Trim(), NoteText.Trim(), SelectedTag?.Name ?? string.Empty),
                    AmountText,
                    NoteText.Trim(),
                    SelectedDate.Date,
                    SelectedExpenseCategory,
                    spendingSource.Id,
                    SelectedTag?.Id ?? 0);
            }
            else
            {
                var primary = splitEntries[0];
                _savedState = new ExpenseDetailSavedState(
                    BuildExpenseName(primary.Input.Name, primary.Input.Note, primary.Tag.Name),
                    primary.Input.Amount,
                    primary.Input.Note,
                    primary.Input.Date,
                    primary.Input.Category,
                    spendingSource.Id,
                    primary.Tag.Id);
            }

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
        _availableSpendingSources.Clear();
        _availableSpendingSources.AddRange(_mainViewModel.BudgetPanel.SpendingSources.Where(source => source.IsEnabled));

        _orderedTags.Clear();
        _orderedTags.AddRange(_mainViewModel.BudgetPanel.Tags
            .Concat(_mainViewModel.BudgetPanel.OtherTags)
            .GroupBy(tag => tag.Id)
            .Select(group => group.First()));

        RefreshTagCollections();
        RefreshSpendingSources();
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

    private void RefreshSpendingSources()
    {
        var selectedSpendingSourceId = SelectedSpendingSource?.Id;
        ReplaceCollection(SpendingSources, _availableSpendingSources
            .OrderBy(source => source.SpendingSourceType)
            .ThenBy(source => source.Name));

        SelectedSpendingSource = selectedSpendingSourceId is null
            ? SpendingSources.FirstOrDefault()
            : SpendingSources.FirstOrDefault(source => source.Id == selectedSpendingSourceId.Value) ??
              SpendingSources.FirstOrDefault();
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

    private static void ApplyExpenseToSpendingSource(SpendingSource spendingSource, decimal amount)
    {
        if (spendingSource.SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL)
        {
            spendingSource.SpentAmount += amount;
            return;
        }

        spendingSource.Balance -= amount;
    }

    private static bool IsBudgetReconciliationExpenseLog(ExpenseLog expenseLog)
    {
        var tag = expenseLog.Expense?.ExpenseTag;
        return tag is { IsSystemTag: true } &&
               string.Equals(tag.Name, SystemExpenseTags.BudgetReconciliationName, StringComparison.OrdinalIgnoreCase);
    }

    private static void RevertExpenseFromSpendingSource(SpendingSource spendingSource, decimal amount)
    {
        if (spendingSource.SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL)
        {
            spendingSource.SpentAmount = Math.Max(0m, spendingSource.SpentAmount - amount);
            return;
        }

        spendingSource.Balance += amount;
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
            expenseLog.Notes?.Trim() ?? string.Empty,
            expenseLog.DeductedOn == default ? DateTime.Today : expenseLog.DeductedOn.Date,
            expenseLog.Expense?.ExpenseCategory ?? ExpenseCategory.Needs,
            expenseLog.SpendingSource?.Id ?? 0,
            expenseLog.Expense?.ExpenseTag?.Id ?? 0);
    }

    private static ExpenseDetailSnapshot CreateMessageSnapshot(ExpenseDetailSavedState savedState)
    {
        return new ExpenseDetailSnapshot(
            savedState.Amount,
            savedState.Date,
            savedState.Category,
            savedState.SpendingSourceId,
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

        if (input.Date.Date != savedState.Date.Date)
            changedFields |= ExpenseDetailChangedFields.Date;

        if (input.Category != savedState.Category)
            changedFields |= ExpenseDetailChangedFields.Category;

        if (input.SpendingSourceId != savedState.SpendingSourceId)
            changedFields |= ExpenseDetailChangedFields.SpendingSource;

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
        int SpendingSourceId,
        DateTime Date,
        string Note,
        ExpenseCategory Category,
        int TagId);

    private readonly record struct ExpenseSplitInput(
        string Name,
        decimal Amount,
        ExpenseCategory Category,
        int TagId,
        string Note,
        DateTime Date);

    private readonly record struct ExpenseDetailSavedState(
        string Name,
        decimal Amount,
        string Note,
        DateTime Date,
        ExpenseCategory Category,
        int SpendingSourceId,
        int TagId);
}
