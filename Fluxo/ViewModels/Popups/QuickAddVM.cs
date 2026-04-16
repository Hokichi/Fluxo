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

public partial class QuickAddVM : ObservableObject
{
    private const int NoSpendingSourceId = -1;
    private const int NoTagId = -1;

    private readonly List<SpendingSourceVM> _availableSpendingSources = [];
    private readonly MainVM _mainViewModel;
    private readonly List<ExpenseTagVM> _orderedTags = [];
    private readonly IUnitOfWork _uow;
    private FormState _initialState;
    private bool _isChangeTrackingInitialized;

    [ObservableProperty] private string _amountText = string.Empty;
    [ObservableProperty] private bool _isExpense = true;
    [ObservableProperty] private bool _isMoreTagsOpen;
    [ObservableProperty] private bool _isSaving;
    private bool _isUpdatingTagCollections;
    [ObservableProperty] private string _nameText = string.Empty;
    [ObservableProperty] private string _noteText = string.Empty;
    [ObservableProperty] private DateTime _selectedDate = DateTime.Today;
    [ObservableProperty] private ExpenseCategory _selectedExpenseCategory = ExpenseCategory.Needs;
    [ObservableProperty] private SpendingSourceVM? _selectedSpendingSource;
    [ObservableProperty] private ExpenseTagVM? _selectedTag;

    public QuickAddVM(MainVM mainViewModel, IUnitOfWork uoW)
    {
        _mainViewModel = mainViewModel;
        _uow = uoW;

        ReloadChoicesFromMainViewModel();
        ResetForm(false);
        _initialState = CaptureState();
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
    public bool CanSave => !IsSaving && AreRequiredFieldsFilled();
    public bool HasChanges => _isChangeTrackingInitialized && !CaptureState().Equals(_initialState);

    public void BeginChangeTracking()
    {
        _initialState = CaptureState();
        _isChangeTrackingInitialized = true;
        NotifyFormStateChanged();
    }

    public bool IsIncome
    {
        get => !IsExpense;
        set
        {
            if (value == IsIncome)
                return;

            IsExpense = !value;
        }
    }

    public bool HasMoreTags => OverflowTags.Count > 0;

    partial void OnAmountTextChanged(string value) => NotifyFormStateChanged();
    partial void OnIsMoreTagsOpenChanged(bool value) => NotifyFormStateChanged();
    partial void OnIsSavingChanged(bool value) => NotifyFormStateChanged();
    partial void OnNameTextChanged(string value) => NotifyFormStateChanged();
    partial void OnNoteTextChanged(string value) => NotifyFormStateChanged();
    partial void OnSelectedDateChanged(DateTime value) => NotifyFormStateChanged();
    partial void OnSelectedExpenseCategoryChanged(ExpenseCategory value) => NotifyFormStateChanged();
    partial void OnSelectedSpendingSourceChanged(SpendingSourceVM? value) => NotifyFormStateChanged();

    public void InitializeFromDraft(QuickAddDraft draft)
    {
        ReloadChoicesFromMainViewModel();

        IsExpense = draft.IsExpense;
        AmountText = draft.AmountText;
        NameText = draft.Name;
        NoteText = draft.Note;
        SelectedDate = draft.Date.Date;
        SelectedExpenseCategory = draft.Category ?? ExpenseCategory.Needs;
        SelectedSpendingSource = draft.SpendingSourceId is null
            ? SpendingSources.FirstOrDefault()
            : SpendingSources.FirstOrDefault(source => source.Id == draft.SpendingSourceId.Value) ??
              SpendingSources.FirstOrDefault();
        SelectedTag = draft.TagId is null
            ? _orderedTags.FirstOrDefault()
            : _orderedTags.FirstOrDefault(tag => tag.Id == draft.TagId.Value) ?? _orderedTags.FirstOrDefault();
        IsMoreTagsOpen = false;
    }

    partial void OnIsExpenseChanged(bool value)
    {
        OnPropertyChanged(nameof(IsIncome));

        if (!value)
            IsMoreTagsOpen = false;

        RefreshSpendingSources();
        NotifyFormStateChanged();
    }

    partial void OnSelectedTagChanged(ExpenseTagVM? value)
    {
        NotifyFormStateChanged();

        if (_isUpdatingTagCollections || value is null)
            return;

        PromoteTagToVisibleStart(value);
        IsMoreTagsOpen = false;
    }

    public async Task<QuickAddSubmissionResult> SaveAsync(bool resetAfterSave)
    {
        if (IsSaving)
            return QuickAddSubmissionResult.Failure("A transaction is already being saved.");

        if (!TryBuildTransactionInput(out var input, out var validationMessage))
            return QuickAddSubmissionResult.Failure(validationMessage);

        IsSaving = true;

        try
        {
            var spendingSource = await _uow.SpendingSources.GetByIdAsync(input.SpendingSourceId);
            if (spendingSource is null)
                return QuickAddSubmissionResult.Failure("Please select a valid spending source.");

            if (input.IsExpense)
            {
                var expenseTag = await _uow.ExpenseTags.GetByIdAsync(input.TagId!.Value);
                if (expenseTag is null)
                    return QuickAddSubmissionResult.Failure("Please select a valid tag.");

                var expense = new Expense
                {
                    Name = BuildExpenseName(input.Name, input.Note, expenseTag.Name),
                    Amount = input.Amount,
                    ExpenseKind = ExpenseKind.Manual,
                    ExpenseCategory = input.Category!.Value,
                    RecurringDate = input.Date.Day,
                    IsActive = false,
                    SpendingSourceId = spendingSource.Id,
                    ExpenseTagId = expenseTag.Id
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

                await _uow.Expenses.AddAsync(expense);
                await _uow.ExpenseLogs.AddAsync(expenseLog);

                ApplyExpenseToSpendingSource(spendingSource, input.Amount);
                _uow.SpendingSources.Update(spendingSource);

                await _uow.SaveChangesAsync();
                WeakReferenceMessenger.Default.Send(
                    new RecordLogMemoryMessage(new AddExpenseLogMemoryAction(new ExpenseLogMemorySnapshot(
                        expense.Id,
                        expenseLog.Id,
                        expense.Name,
                        expenseLog.Amount,
                        expense.ExpenseKind,
                        expense.ExpenseCategory,
                        expense.RecurringDate,
                        expense.IsActive,
                        spendingSource.Id,
                        expenseTag.Id,
                        expenseLog.DeductedOn,
                        expenseLog.Notes,
                        expenseLog.IsForDeletion))));
            }
            else
            {
                var incomeLog = new IncomeLog
                {
                    Amount = input.Amount,
                    AddedOn = input.Date,
                    Notes = BuildIncomeNote(input.Name, input.Note),
                    SpendingSourceId = spendingSource.Id
                };

                await _uow.IncomeLogs.AddAsync(incomeLog);

                ApplyIncomeToSpendingSource(spendingSource, input.Amount);
                _uow.SpendingSources.Update(spendingSource);

                await _uow.SaveChangesAsync();
                WeakReferenceMessenger.Default.Send(
                    new RecordLogMemoryMessage(new AddIncomeLogMemoryAction(new IncomeLogMemorySnapshot(
                        incomeLog.Id,
                        spendingSource.Id,
                        incomeLog.Amount,
                        incomeLog.AddedOn,
                        incomeLog.Notes))));
            }

            WeakReferenceMessenger.Default.Send(new DashboardDataInvalidatedMessage(
                DashboardDataInvalidationScope.Budget | DashboardDataInvalidationScope.Notifications));

            await _mainViewModel.ReloadCurrentDataAsync();

            if (resetAfterSave)
            {
                ReloadChoicesFromMainViewModel();
                ResetForm(true);
            }

            return QuickAddSubmissionResult.Success();
        }
        catch (Exception exception)
        {
            return QuickAddSubmissionResult.Failure($"Unable to save the transaction.\n\n{exception.Message}");
        }
        finally
        {
            IsSaving = false;
        }
    }

    public bool HasValidEntryToPersistOnClose()
    {
        return TryBuildTransactionInput(out _, out _);
    }

    public void ResetForm(bool keepCurrentType)
    {
        if (!keepCurrentType)
            IsExpense = true;

        AmountText = string.Empty;
        NameText = string.Empty;
        NoteText = string.Empty;
        SelectedDate = DateTime.Today;
        SelectedExpenseCategory = ExpenseCategory.Needs;
        SelectedSpendingSource = SpendingSources.FirstOrDefault();
        SelectedTag = _orderedTags.FirstOrDefault();
        IsMoreTagsOpen = false;
    }

    private bool TryBuildTransactionInput(out QuickTransactionInput input, out string validationMessage)
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

        ExpenseCategory? category = null;
        int? tagId = null;

        if (IsExpense)
        {
            if (SelectedTag is null)
            {
                validationMessage = "Please choose a tag.";
                return false;
            }

            category = SelectedExpenseCategory;
            tagId = SelectedTag.Id;
        }

        input = new QuickTransactionInput(
            IsExpense,
            NameText.Trim(),
            amount,
            SelectedSpendingSource.Id,
            SelectedDate.Date,
            NoteText.Trim(),
            category,
            tagId);

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

    private static string BuildIncomeNote(string name, string note)
    {
        var trimmedName = name.Trim();
        var trimmedNote = note.Trim();

        if (string.IsNullOrWhiteSpace(trimmedName))
            return trimmedNote;

        if (string.IsNullOrWhiteSpace(trimmedNote))
            return trimmedName;

        return $"{trimmedName}\n{trimmedNote}";
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

    private static void ApplyIncomeToSpendingSource(SpendingSource spendingSource, decimal amount)
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

    private bool AreRequiredFieldsFilled()
    {
        if (string.IsNullOrWhiteSpace(NameText) || string.IsNullOrWhiteSpace(AmountText))
            return false;

        if (SelectedSpendingSource is null)
            return false;

        if (IsExpense && SelectedTag is null)
            return false;

        return true;
    }

    private FormState CaptureState()
    {
        return new FormState(
            IsExpense,
            NameText ?? string.Empty,
            AmountText ?? string.Empty,
            NoteText ?? string.Empty,
            SelectedDate.Date,
            SelectedExpenseCategory,
            SelectedSpendingSource?.Id ?? NoSpendingSourceId,
            SelectedTag?.Id ?? NoTagId);
    }

    private void NotifyFormStateChanged()
    {
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(HasChanges));
    }

    private void RefreshSpendingSources()
    {
        var selectedSpendingSourceId = SelectedSpendingSource?.Id;

        var filteredSources = _availableSpendingSources
            .Where(source =>
                IsExpense || source.SpendingSourceType is not (SpendingSourceType.Credit or SpendingSourceType.BNPL))
            .OrderBy(source => source.Name)
            .ToList();

        ReplaceCollection(SpendingSources, filteredSources);

        SelectedSpendingSource = selectedSpendingSourceId is null
            ? SpendingSources.FirstOrDefault()
            : SpendingSources.FirstOrDefault(source => source.Id == selectedSpendingSourceId.Value) ??
              SpendingSources.FirstOrDefault();
    }

    public sealed record ExpenseCategoryOption(string Label, ExpenseCategory Value);

    public readonly record struct QuickAddSubmissionResult(bool IsSuccess, string? ErrorMessage)
    {
        public static QuickAddSubmissionResult Success()
        {
            return new QuickAddSubmissionResult(true, null);
        }

        public static QuickAddSubmissionResult Failure(string? errorMessage)
        {
            return new QuickAddSubmissionResult(false, errorMessage);
        }
    }

    public readonly record struct QuickAddDraft(
        bool IsExpense,
        string Name,
        string AmountText,
        int? SpendingSourceId,
        DateTime Date,
        string Note,
        ExpenseCategory? Category,
        int? TagId);

    private readonly record struct QuickTransactionInput(
        bool IsExpense,
        string Name,
        decimal Amount,
        int SpendingSourceId,
        DateTime Date,
        string Note,
        ExpenseCategory? Category,
        int? TagId);

    private readonly record struct FormState(
        bool IsExpense,
        string NameText,
        string AmountText,
        string NoteText,
        DateTime SelectedDate,
        ExpenseCategory SelectedExpenseCategory,
        int SelectedSpendingSourceId,
        int SelectedTagId);
}
