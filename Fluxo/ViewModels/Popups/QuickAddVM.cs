using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Resources.Messages;
using Fluxo.Services.History;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Helpers;
using Fluxo.ViewModels.Shell;
using MainVM = Fluxo.ViewModels.Shell.Main.MainVM;

namespace Fluxo.ViewModels.Popups;

public partial class QuickAddVM : ObservableObject
{
    private const int NoSpendingSourceId = -1;
    private const int NoTagId = -1;
    private const int NoSavingGoalId = -1;

    private readonly List<SpendingSourceVM> _availableSpendingSources = [];
    private readonly List<SavingGoalVM> _orderedGoals = [];
    private readonly MainVM _mainViewModel;
    private readonly List<ExpenseTagVM> _orderedTags = [];
    private readonly IAppDataService _appData;
    private FormState _initialState;
    private bool _isChangeTrackingInitialized;

    [ObservableProperty] private decimal _amountText;
    [ObservableProperty] private bool _isExpense = true;
    [ObservableProperty] private bool _isGoal;
    [ObservableProperty] private bool _isMoreTagsOpen;
    [ObservableProperty] private bool _isSaving;
    private bool _isUpdatingTagCollections;
    [ObservableProperty] private string _nameText = string.Empty;
    [ObservableProperty] private string _noteText = string.Empty;
    [ObservableProperty] private DateTime _selectedDate = DateTime.Today;
    [ObservableProperty] private ExpenseCategory _selectedExpenseCategory = ExpenseCategory.Needs;
    [ObservableProperty] private SavingGoalVM? _selectedGoal;
    [ObservableProperty] private SpendingSourceVM? _selectedSpendingSource;
    [ObservableProperty] private ExpenseTagVM? _selectedTag;

    public QuickAddVM(MainVM mainViewModel, IAppDataService appData)
    {
        _mainViewModel = mainViewModel;
        _appData = appData;
        SpendingSourcesView = SpendingSourceComboBoxViewFactory.CreateGroupedByTypeThenName(
            SpendingSources,
            nameof(SpendingSourceVM.TypeDisplayName),
            nameof(SpendingSourceVM.SpendingSourceType),
            nameof(SpendingSourceVM.Name));

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
    public ICollectionView SpendingSourcesView { get; }
    public ObservableCollection<SavingGoalVM> Goals { get; } = [];
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
        get => !IsExpense && !IsGoal;
        set
        {
            if (value == IsIncome)
                return;

            if (value)
            {
                IsGoal = false;
                IsExpense = false;
            }
            else if (IsIncome)
            {
                IsExpense = true;
            }
        }
    }

    public bool HasMoreTags => OverflowTags.Count > 0;

    partial void OnAmountTextChanged(decimal value) => NotifyFormStateChanged();
    partial void OnIsMoreTagsOpenChanged(bool value) => NotifyFormStateChanged();
    partial void OnIsSavingChanged(bool value) => NotifyFormStateChanged();
    partial void OnNameTextChanged(string value) => NotifyFormStateChanged();
    partial void OnNoteTextChanged(string value) => NotifyFormStateChanged();
    partial void OnSelectedDateChanged(DateTime value) => NotifyFormStateChanged();
    partial void OnSelectedExpenseCategoryChanged(ExpenseCategory value) => NotifyFormStateChanged();
    partial void OnSelectedGoalChanged(SavingGoalVM? value) => NotifyFormStateChanged();
    partial void OnSelectedSpendingSourceChanged(SpendingSourceVM? value) => NotifyFormStateChanged();

    public void InitializeFromDraft(QuickAddDraft draft)
    {
        ReloadChoicesFromMainViewModel();

        IsExpense = draft.IsExpense;
        IsGoal = draft.IsGoal;
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
        SelectedGoal = draft.GoalId is null
            ? Goals.FirstOrDefault()
            : Goals.FirstOrDefault(goal => goal.Id == draft.GoalId.Value) ?? Goals.FirstOrDefault();
        IsMoreTagsOpen = false;
    }

    partial void OnIsExpenseChanged(bool value)
    {
        if (value && IsGoal)
            IsGoal = false;

        OnPropertyChanged(nameof(IsIncome));

        if (!value || IsGoal)
            IsMoreTagsOpen = false;

        RefreshSpendingSources();
        NotifyFormStateChanged();
    }

    partial void OnIsGoalChanged(bool value)
    {
        if (value && IsExpense)
            IsExpense = false;

        OnPropertyChanged(nameof(IsIncome));

        if (value)
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
            var spendingSource = await _appData.GetSpendingSourceByIdAsync(input.SpendingSourceId);
            if (spendingSource is null)
                return QuickAddSubmissionResult.Failure("Please select a valid spending source.");

            var invalidationScope = DashboardDataInvalidationScope.Budget | DashboardDataInvalidationScope.Notifications;

            if (input.IsGoal)
            {
                if (input.GoalId is null)
                    return QuickAddSubmissionResult.Failure("Please choose a goal.");

                if (!GoalUpdateTransactionSupport.IsEligibleGoalSourceType(spendingSource.SpendingSourceType))
                    return QuickAddSubmissionResult.Failure("Goal updates can only be taken from Cash or Checking.");

                var goal = await _appData.GetSavingGoalByIdAsync(input.GoalId.Value);
                if (goal is null)
                    return QuickAddSubmissionResult.Failure("Please select a valid goal.");

                var goalUpdateTag = await GoalUpdateTransactionSupport.ResolveGoalUpdateTagAsync(_appData);
                var expense = new Expense
                {
                    Name = BuildGoalUpdateName(goal.Name),
                    Amount = input.Amount,
                    ExpenseKind = ExpenseKind.Manual,
                    ExpenseCategory = ExpenseCategory.Savings,
                    RecurringDate = input.Date.Day,
                    IsActive = false,
                    SpendingSourceId = spendingSource.Id,
                    ExpenseTagId = goalUpdateTag.Id
                };

                var expenseLog = new ExpenseLog
                {
                    Expense = expense,
                    Amount = input.Amount,
                    DeductedOn = input.Date,
                    Notes = $"Goal update for {goal.Name}",
                    IsForDeletion = false,
                    SpendingSourceId = spendingSource.Id
                };

                goal.CurrentAmount += input.Amount;

                await _appData.AddExpenseAsync(expense);
                await _appData.AddExpenseLogAsync(expenseLog);
                _appData.UpdateSavingGoal(goal);

                ApplyExpenseToSpendingSource(spendingSource, input.Amount);
                _appData.UpdateSpendingSource(spendingSource);

                await _appData.SaveChangesAsync();
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
                        goalUpdateTag.Id,
                        expenseLog.DeductedOn,
                        expenseLog.Notes,
                        expenseLog.IsForDeletion))));

                invalidationScope |= DashboardDataInvalidationScope.SavingGoals;
            }
            else if (input.IsExpense)
            {
                var expenseTag = await _appData.GetExpenseTagByIdAsync(input.TagId!.Value);
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

                await _appData.AddExpenseAsync(expense);
                await _appData.AddExpenseLogAsync(expenseLog);

                ApplyExpenseToSpendingSource(spendingSource, input.Amount);
                _appData.UpdateSpendingSource(spendingSource);

                await _appData.SaveChangesAsync();
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

                await _appData.AddIncomeLogAsync(incomeLog);

                ApplyIncomeToSpendingSource(spendingSource, input.Amount);
                _appData.UpdateSpendingSource(spendingSource);

                await _appData.SaveChangesAsync();
                WeakReferenceMessenger.Default.Send(
                    new RecordLogMemoryMessage(new AddIncomeLogMemoryAction(new IncomeLogMemorySnapshot(
                        incomeLog.Id,
                        spendingSource.Id,
                        incomeLog.Amount,
                        incomeLog.AddedOn,
                        incomeLog.Notes))));
            }

            WeakReferenceMessenger.Default.Send(new DashboardDataInvalidatedMessage(invalidationScope));

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
        {
            IsExpense = true;
            IsGoal = false;
        }

        AmountText = 0m;
        NameText = string.Empty;
        NoteText = string.Empty;
        SelectedDate = DateTime.Today;
        SelectedExpenseCategory = ExpenseCategory.Needs;
        SelectedSpendingSource = SpendingSources.FirstOrDefault();
        SelectedTag = _orderedTags.FirstOrDefault();
        SelectedGoal = Goals.FirstOrDefault();
        IsMoreTagsOpen = false;
    }

    private bool TryBuildTransactionInput(out QuickTransactionInput input, out string validationMessage)
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

        ExpenseCategory? category = null;
        int? tagId = null;
        int? goalId = null;

        if (IsGoal)
        {
            if (SelectedGoal is null)
            {
                validationMessage = "Please choose a goal.";
                return false;
            }

            goalId = SelectedGoal.Id;
        }
        else if (IsExpense)
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
            IsGoal,
            NameText.Trim(),
            AmountText,
            SelectedSpendingSource.Id,
            SelectedDate.Date,
            NoteText.Trim(),
            category,
            tagId,
            goalId);

        return true;
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

        _orderedGoals.Clear();
        _orderedGoals.AddRange(_mainViewModel.SavingGoalsPanel.SavingGoals
            .GroupBy(goal => goal.Id)
            .Select(group => group.First())
            .OrderBy(goal => goal.Name));

        ReplaceCollection(Goals, _orderedGoals);
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

    private static string BuildGoalUpdateName(string goalName)
    {
        var trimmedGoalName = goalName.Trim();
        return string.IsNullOrWhiteSpace(trimmedGoalName)
            ? GoalUpdateTransactionSupport.GoalUpdateTagName
            : $"{GoalUpdateTransactionSupport.GoalUpdateTagName}: {trimmedGoalName}";
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
        if (AmountText <= 0m)
            return false;

        if (SelectedSpendingSource is null)
            return false;

        if (IsGoal)
            return SelectedGoal is not null;

        if (string.IsNullOrWhiteSpace(NameText))
            return false;

        if (IsExpense && SelectedTag is null)
            return false;

        return true;
    }

    private FormState CaptureState()
    {
        return new FormState(
            IsExpense,
            IsGoal,
            NameText ?? string.Empty,
            AmountText,
            NoteText ?? string.Empty,
            SelectedDate.Date,
            SelectedExpenseCategory,
            SelectedSpendingSource?.Id ?? NoSpendingSourceId,
            SelectedTag?.Id ?? NoTagId,
            SelectedGoal?.Id ?? NoSavingGoalId);
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
                IsGoal
                    ? GoalUpdateTransactionSupport.IsEligibleGoalSourceType(source.SpendingSourceType)
                    : IsExpense || source.SpendingSourceType is not (SpendingSourceType.Credit or SpendingSourceType.BNPL))
            .OrderBy(source => source.SpendingSourceType)
            .ThenBy(source => source.Name)
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
        decimal AmountText,
        int? SpendingSourceId,
        DateTime Date,
        string Note,
        ExpenseCategory? Category,
        int? TagId,
        bool IsGoal = false,
        int? GoalId = null);

    private readonly record struct QuickTransactionInput(
        bool IsExpense,
        bool IsGoal,
        string Name,
        decimal Amount,
        int SpendingSourceId,
        DateTime Date,
        string Note,
        ExpenseCategory? Category,
        int? TagId,
        int? GoalId);

    private readonly record struct FormState(
        bool IsExpense,
        bool IsGoal,
        string NameText,
        decimal AmountText,
        string NoteText,
        DateTime SelectedDate,
        ExpenseCategory SelectedExpenseCategory,
        int SelectedSpendingSourceId,
        int SelectedTagId,
        int SelectedGoalId);
}
