using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
using Fluxo.Services.History;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Messages;
using Fluxo.ViewModels.Shell;

namespace Fluxo.ViewModels.Popups;

public partial class ExpenseDetailVM : ObservableObject
{
    private readonly List<SpendingSourceVM> _availableSpendingSources = [];
    private readonly ExpenseLogVM _expenseLog;
    private readonly MainVM _mainViewModel;
    private readonly List<ExpenseTagVM> _orderedTags = [];
    private readonly IUnitOfWork _uow;

    [ObservableProperty] private string _amountText = string.Empty;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private bool _isMoreTagsOpen;
    [ObservableProperty] private bool _isSaving;
    private bool _isUpdatingTagCollections;
    [ObservableProperty] private string _nameText = string.Empty;
    [ObservableProperty] private string _noteText = string.Empty;
    [ObservableProperty] private string _popupTitle = "Expense Detail";

    private ExpenseDetailSavedState _savedState = new(string.Empty, 0m, string.Empty, DateTime.Today,
        ExpenseCategory.Needs, 0, 0);

    [ObservableProperty] private DateTime _selectedDate = DateTime.Today;
    [ObservableProperty] private ExpenseCategory _selectedExpenseCategory = ExpenseCategory.Needs;
    [ObservableProperty] private SpendingSourceVM? _selectedSpendingSource;
    [ObservableProperty] private ExpenseTagVM? _selectedTag;

    public ExpenseDetailVM(MainVM mainViewModel, ExpenseLogVM expenseLog, IUnitOfWork uow)
    {
        _mainViewModel = mainViewModel;
        _expenseLog = expenseLog;
        _uow = uow;

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
    public ObservableCollection<ExpenseTagVM> VisibleTags { get; } = [];
    public ObservableCollection<ExpenseTagVM> OverflowTags { get; } = [];

    public bool AreFieldsReadOnly => !IsEditing;
    public bool CanEditFields => IsEditing;
    public bool HasMoreTags => OverflowTags.Count > 0;

    partial void OnIsEditingChanged(bool value)
    {
        OnPropertyChanged(nameof(AreFieldsReadOnly));
        OnPropertyChanged(nameof(CanEditFields));

        if (!value)
            IsMoreTagsOpen = false;
    }

    partial void OnSelectedTagChanged(ExpenseTagVM? value)
    {
        if (_isUpdatingTagCollections || value is null || !IsEditing)
            return;

        PromoteTagToVisibleStart(value);
        IsMoreTagsOpen = false;
    }

    public void BeginEditing()
    {
        IsEditing = true;
    }

    public void CancelEditing()
    {
        IsEditing = false;
        LoadFromSavedState();
    }

    public QuickAddVM.QuickAddDraft CreateQuickAddDraft()
    {
        return new QuickAddVM.QuickAddDraft(
            true,
            NameText,
            AmountText,
            SelectedSpendingSource?.Id,
            SelectedDate.Date,
            NoteText,
            SelectedExpenseCategory,
            SelectedTag?.Id);
    }

    public async Task<ExpenseDetailSaveResult> SaveAsync()
    {
        if (IsSaving)
            return ExpenseDetailSaveResult.Failure("This expense is already being saved.");

        if (!TryBuildInput(out var input, out var validationMessage))
            return ExpenseDetailSaveResult.Failure(validationMessage);

        var previousState = CreateMessageSnapshot(_savedState);
        var changedFields = GetChangedFields(input, _savedState);
        if (changedFields == ExpenseDetailChangedFields.None)
        {
            IsEditing = false;
            LoadFromSavedState();
            return ExpenseDetailSaveResult.Success();
        }

        IsSaving = true;

        try
        {
            var expenseLog = await _uow.ExpenseLogs.GetByIdAsync(_expenseLog.Id);
            if (expenseLog?.Expense is null)
                return ExpenseDetailSaveResult.Failure("Unable to load this expense.");

            var beforeHistorySnapshot = ExpenseLogMemorySnapshot.Create(expenseLog);

            var expense = expenseLog.Expense;
            var currentSpendingSource = expenseLog.SpendingSource;
            var newSpendingSource = await _uow.SpendingSources.GetByIdAsync(input.SpendingSourceId);
            if (newSpendingSource is null)
                return ExpenseDetailSaveResult.Failure("Please select a valid spending source.");

            var expenseTag = await _uow.ExpenseTags.GetByIdAsync(input.TagId);
            if (expenseTag is null)
                return ExpenseDetailSaveResult.Failure("Please select a valid tag.");

            var resolvedName = BuildExpenseName(input.Name, input.Note, expenseTag.Name);

            RevertExpenseFromSpendingSource(currentSpendingSource, expenseLog.Amount);
            ApplyExpenseToSpendingSource(newSpendingSource, input.Amount);

            expense.Name = resolvedName;
            expense.Amount = input.Amount;
            expense.ExpenseCategory = input.Category;
            expense.RecurringDate = input.Date;
            expense.SpendingSource = newSpendingSource;
            expense.ExpenseTag = expenseTag;

            expenseLog.Amount = input.Amount;
            expenseLog.DeductedOn = input.Date;
            expenseLog.Notes = input.Note;
            expenseLog.SpendingSource = newSpendingSource;

            _uow.Expenses.Update(expense);
            _uow.ExpenseLogs.Update(expenseLog);
            _uow.SpendingSources.Update(currentSpendingSource);

            if (!ReferenceEquals(currentSpendingSource, newSpendingSource))
                _uow.SpendingSources.Update(newSpendingSource);

            await _uow.SaveChangesAsync();
            _savedState = new ExpenseDetailSavedState(
                resolvedName,
                input.Amount,
                input.Note,
                input.Date,
                input.Category,
                input.SpendingSourceId,
                input.TagId);

            IsEditing = false;
            LoadFromSavedState();
            WeakReferenceMessenger.Default.Send(new ExpenseDetailUpdatedMessage(
                new ExpenseDetailUpdate(_expenseLog.Id, previousState, changedFields)));
            WeakReferenceMessenger.Default.Send(new RecordLogMemoryMessage(
                new EditExpenseLogMemoryAction(beforeHistorySnapshot, ExpenseLogMemorySnapshot.Create(expenseLog))));
            return ExpenseDetailSaveResult.Success();
        }
        catch (Exception exception)
        {
            return ExpenseDetailSaveResult.Failure($"Unable to save this expense.\n\n{exception.Message}");
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

        if (!TryBuildInput(out var input, out _))
            return false;

        return GetChangedFields(input, _savedState) != ExpenseDetailChangedFields.None;
    }

    private void LoadFromSavedState()
    {
        AmountText = _savedState.Amount.ToString(CultureInfo.CurrentCulture);
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

        if (!TryParseAmount(out var amount))
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
            amount,
            SelectedSpendingSource.Id,
            SelectedDate.Date,
            NoteText.Trim(),
            SelectedExpenseCategory,
            SelectedTag.Id);

        return true;
    }

    private bool TryParseAmount(out decimal amount)
    {
        amount = 0m;

        var normalizedAmount = AmountText
            .Trim()
            .Replace(CultureInfo.CurrentCulture.NumberFormat.CurrencySymbol, string.Empty, StringComparison.Ordinal)
            .Replace(",", string.Empty, StringComparison.Ordinal)
            .Trim();

        if (string.IsNullOrWhiteSpace(normalizedAmount))
            return false;

        if (!decimal.TryParse(normalizedAmount, NumberStyles.Number | NumberStyles.AllowCurrencySymbol,
                CultureInfo.CurrentCulture, out amount) &&
            !decimal.TryParse(normalizedAmount, NumberStyles.Number | NumberStyles.AllowCurrencySymbol,
                CultureInfo.InvariantCulture, out amount))
            return false;

        return amount > 0m;
    }

    private void ReloadChoicesFromMainViewModel()
    {
        _availableSpendingSources.Clear();
        _availableSpendingSources.AddRange(_mainViewModel.SpendingSources);

        _orderedTags.Clear();
        _orderedTags.AddRange(_mainViewModel.Tags
            .Concat(_mainViewModel.OtherTags)
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
            ReplaceCollection(VisibleTags, _orderedTags.Take(4));
            ReplaceCollection(OverflowTags, _orderedTags.Skip(4));

            OnPropertyChanged(nameof(HasMoreTags));
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
        ReplaceCollection(SpendingSources, _availableSpendingSources.OrderBy(source => source.Name));

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

    private readonly record struct ExpenseDetailSavedState(
        string Name,
        decimal Amount,
        string Note,
        DateTime Date,
        ExpenseCategory Category,
        int SpendingSourceId,
        int TagId);
}