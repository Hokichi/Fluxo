using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Shell;

namespace Fluxo.ViewModels.Popups;

public partial class QuickAddVM : ObservableObject
{
    private readonly MainVM _mainViewModel;
    private readonly List<ExpenseTagVM> _orderedTags = [];
    private readonly IUnitOfWork _uow;
    private bool _isUpdatingTagCollections;

    [ObservableProperty] private string _amountText = string.Empty;
    [ObservableProperty] private bool _isExpense = true;
    [ObservableProperty] private bool _isMoreTagsOpen;
    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private string _noteText = string.Empty;
    [ObservableProperty] private DateTime _selectedDate = DateTime.Today;
    [ObservableProperty] private ExpenseCategory _selectedExpenseCategory = ExpenseCategory.Needs;
    [ObservableProperty] private SpendingSourceVM? _selectedSpendingSource;
    [ObservableProperty] private ExpenseTagVM? _selectedTag;

    public IReadOnlyList<ExpenseCategoryOption> ExpenseCategories { get; } =
    [
        new("Needs", ExpenseCategory.Needs),
        new("Wants", ExpenseCategory.Wants),
        new("Invest", ExpenseCategory.Savings)
    ];

    public ObservableCollection<SpendingSourceVM> SpendingSources { get; } = [];
    public ObservableCollection<ExpenseTagVM> VisibleTags { get; } = [];
    public ObservableCollection<ExpenseTagVM> OverflowTags { get; } = [];

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

    public QuickAddVM(MainVM mainViewModel, IUnitOfWork uoW)
    {
        _mainViewModel = mainViewModel;
        _uow = uoW;

        ReloadChoicesFromMainViewModel();
        ResetForm(keepCurrentType: false);
    }

    partial void OnIsExpenseChanged(bool value)
    {
        OnPropertyChanged(nameof(IsIncome));

        if (!value)
            IsMoreTagsOpen = false;
    }

    partial void OnSelectedTagChanged(ExpenseTagVM? value)
    {
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
                    Name = BuildExpenseName(input.Note, expenseTag.Name),
                    Amount = input.Amount,
                    ExpenseKind = ExpenseKind.Manual,
                    ExpenseCategory = input.Category!.Value,
                    RecurringDate = input.Date,
                    IsActive = false,
                    SpendingSource = spendingSource,
                    ExpenseTag = expenseTag
                };

                var expenseLog = new ExpenseLog
                {
                    Expense = expense,
                    SpendingSource = spendingSource,
                    Amount = input.Amount,
                    DeductedOn = input.Date,
                    Notes = input.Note,
                    IsForDeletion = false
                };

                await _uow.Expenses.AddAsync(expense);
                await _uow.ExpenseLogs.AddAsync(expenseLog);

                ApplyExpenseToSpendingSource(spendingSource, input.Amount);
                _uow.SpendingSources.Update(spendingSource);
            }
            else
            {
                var incomeLog = new IncomeLog
                {
                    SpendingSource = spendingSource,
                    Amount = input.Amount,
                    AddedOn = input.Date,
                    Notes = input.Note
                };

                await _uow.IncomeLogs.AddAsync(incomeLog);

                ApplyIncomeToSpendingSource(spendingSource, input.Amount);
                _uow.SpendingSources.Update(spendingSource);
            }

            await _uow.SaveChangesAsync();
            await _mainViewModel.ReloadCurrentDataAsync();

            if (resetAfterSave)
            {
                ReloadChoicesFromMainViewModel();
                ResetForm(keepCurrentType: true);
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

    public void ResetForm(bool keepCurrentType)
    {
        if (!keepCurrentType)
            IsExpense = true;

        AmountText = string.Empty;
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
        ReplaceCollection(
            SpendingSources,
            _mainViewModel.SpendingSources
                .OrderBy(source => source.Name)
                .ToList());

        _orderedTags.Clear();
        _orderedTags.AddRange(_mainViewModel.Tags
            .Concat(_mainViewModel.OtherTags)
            .GroupBy(tag => tag.Id)
            .Select(group => group.First()));

        RefreshTagCollections();
        SelectedSpendingSource ??= SpendingSources.FirstOrDefault();
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

    private static string BuildExpenseName(string note, string fallbackName)
    {
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

    public sealed record ExpenseCategoryOption(string Label, ExpenseCategory Value);

    public readonly record struct QuickAddSubmissionResult(bool IsSuccess, string? ErrorMessage)
    {
        public static QuickAddSubmissionResult Success() => new(true, null);
        public static QuickAddSubmissionResult Failure(string? errorMessage) => new(false, errorMessage);
    }

    private readonly record struct QuickTransactionInput(
        bool IsExpense,
        decimal Amount,
        int SpendingSourceId,
        DateTime Date,
        string Note,
        ExpenseCategory? Category,
        int? TagId);
}
