using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Budgeting;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Resources.Resources.Messages;
using Fluxo.Services.History;
using Fluxo.Services.Logging;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Popups.Helpers;
using Fluxo.ViewModels.Shell;
using System.Globalization;
using MainVM = Fluxo.ViewModels.Shell.Main.MainVM;

namespace Fluxo.ViewModels.Popups;

public partial class AddNewTransactionVM : ObservableValidator
{
    private const int DefaultVisibleTagSlots = 4;
    private const int MaxNameLength = 256;
    private const int NoSpendingSourceId = -1;
    private const int NoTagId = -1;
    private const int NoSavingGoalId = -1;
    private const decimal SimilarAmountTolerance = 0.05m;

    private readonly List<SpendingSourceVM> _availableSpendingSources = [];
    private readonly IReadOnlyList<SpendingSourceVM>? _spendingSourcesOverride;
    private readonly Func<RecurringDraftSaveInput, Task<AddNewTransactionSubmissionResult>>? _saveRecurringDraftAsync;
    private readonly List<SavingGoalVM> _orderedGoals = [];
    private readonly MainVM _mainViewModel;
    private readonly List<ExpenseTagVM> _orderedTags = [];
    private readonly IAppDataService _appData;
    private FormState _initialState;
    private bool _isChangeTrackingInitialized;
    private bool _isAmountValidationActive;
    private bool _isNameValidationActive;
    private int _transactionNameSuggestionRequestVersion;

    [ObservableProperty]
    [CustomValidation(typeof(AddNewTransactionVM), nameof(ValidateAmountText))]
    private decimal _amountText;
    [ObservableProperty] private bool _isExpense = true;
    [ObservableProperty] private bool _isGoal;
    [ObservableProperty] private bool _isMoreTagsOpen;
    [ObservableProperty] private bool _isSaving;
    private bool _isUpdatingTagCollections;
    private int _visibleTagSlots = DefaultVisibleTagSlots;
    [ObservableProperty]
    [CustomValidation(typeof(AddNewTransactionVM), nameof(ValidateNameText))]
    private string _nameText = string.Empty;
    [ObservableProperty] private string _noteText = string.Empty;
    [ObservableProperty] private DateTime _selectedDate = DateTime.Today;
    [ObservableProperty] private bool _isRecurring;
    [ObservableProperty] private bool _isPinned;
    [ObservableProperty] private bool _isHistoryOpen;
    [ObservableProperty] private AddNewTransactionHistoryItemVM? _selectedPinnedHistoryItem;
    [ObservableProperty] private AddNewTransactionHistoryItemVM? _selectedHistoryItem;
    [ObservableProperty] private RecurringPeriod _selectedRecurringPeriod = RecurringPeriod.Monthly;
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(AddNewTransactionVM), nameof(ValidateRecurringTimeText))]
    private string _recurringTimeText = string.Empty;
    [ObservableProperty] private bool _isRecurringModeLocked;
    [ObservableProperty] private ExpenseCategory _selectedExpenseCategory = ExpenseCategory.Needs;
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(AddNewTransactionVM), nameof(ValidateSelectedGoal))]
    private SavingGoalVM? _selectedGoal;
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(AddNewTransactionVM), nameof(ValidateSelectedSpendingSource))]
    private SpendingSourceVM? _selectedSpendingSource;
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(AddNewTransactionVM), nameof(ValidateSelectedTag))]
    private ExpenseTagVM? _selectedTag;

    public AddNewTransactionVM(
        MainVM mainViewModel,
        IAppDataService appData,
        IReadOnlyList<SpendingSourceVM>? spendingSourcesOverride = null,
        Func<RecurringDraftSaveInput, Task<AddNewTransactionSubmissionResult>>? saveRecurringDraftAsync = null)
    {
        _mainViewModel = mainViewModel;
        _appData = appData;
        _spendingSourcesOverride = spendingSourcesOverride;
        _saveRecurringDraftAsync = saveRecurringDraftAsync;
        ErrorsChanged += (_, e) =>
        {
            OnPropertyChanged(nameof(CanSave));

            if (e.PropertyName == nameof(NameText))
                OnPropertyChanged(nameof(NameValidationHint));

            if (e.PropertyName == nameof(AmountText))
                OnPropertyChanged(nameof(AmountValidationHint));
        };
        SpendingSourcesView = SpendingSourceComboBoxViewFactory.CreateGroupedByProperty(
            SpendingSources,
            nameof(SpendingSourceVM.TypeDisplayName));

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
    public ObservableCollection<AddNewTransactionSuggestion> TransactionNameSuggestions { get; } = [];
    public AddNewTransactionHistoryListVM PinnedHistory { get; } = new();
    public AddNewTransactionHistoryListVM TransactionHistory { get; } = new();
    public IReadOnlyList<RecurringPeriod> RecurringPeriods { get; } =
    [
        RecurringPeriod.None,
        RecurringPeriod.Weekly,
        RecurringPeriod.Biweekly,
        RecurringPeriod.Monthly
    ];
    public IReadOnlyList<RecurringTimeOption> WeekdayOptions { get; } =
    [
        new("Monday", "1"),
        new("Tuesday", "2"),
        new("Wednesday", "3"),
        new("Thursday", "4"),
        new("Friday", "5"),
        new("Saturday", "6"),
        new("Sunday", "7")
    ];
    public bool CanSave => !IsSaving && IsCurrentInputValid();
    public bool HasChanges => _isChangeTrackingInitialized && HasPendingTransactionInputChanges();
    public bool HasTransactionNameSuggestions => TransactionNameSuggestions.Count > 0;
    public bool ShowRecurringDayInput => IsRecurring;
    public bool ShowRecurringNoneInput => IsRecurring && SelectedRecurringPeriod == RecurringPeriod.None;
    public bool ShowRecurringWeekdayInput => IsRecurring && IsWeekdayRecurringPeriod(SelectedRecurringPeriod);
    public bool ShowRecurringMonthlyInput => IsRecurring && SelectedRecurringPeriod == RecurringPeriod.Monthly;
    public bool ShowDateSelector => !IsRecurring;
    public string DateOrRecurrenceLabel => IsRecurring ? "Recurrence" : "Date";
    public bool CanToggleRecurring => !IsRecurringModeLocked;
    public bool CanUseHistory => !IsGoal;
    public string NameValidationHint => GetValidationHint(nameof(NameText));
    public string AmountValidationHint => GetValidationHint(nameof(AmountText));

    public void BeginChangeTracking()
    {
        _initialState = CaptureState();
        _isChangeTrackingInitialized = true;
        NotifyFormStateChanged();
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

        var persistedTags = ProjectNonSystemTags(allTags).ToList();

        if (persistedTags.Count == 0)
            return;

        var selectedTagId = SelectedTag?.Id;

        _orderedTags.Clear();
        _orderedTags.AddRange(persistedTags);

        RefreshTagCollections();
        SelectedTag = selectedTagId is null
            ? _orderedTags.FirstOrDefault()
            : _orderedTags.FirstOrDefault(tag => tag.Id == selectedTagId.Value) ?? _orderedTags.FirstOrDefault();
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

    partial void OnAmountTextChanged(decimal value)
    {
        RefreshActiveValidation(nameof(AmountText));
        NotifyFormStateChanged();
    }
    partial void OnIsRecurringChanged(bool value)
    {
        if (value && string.IsNullOrWhiteSpace(RecurringTimeText))
            RecurringTimeText = GetDefaultRecurringTimeText(SelectedRecurringPeriod);

        OnPropertyChanged(nameof(ShowRecurringDayInput));
        OnPropertyChanged(nameof(ShowRecurringNoneInput));
        OnPropertyChanged(nameof(ShowRecurringWeekdayInput));
        OnPropertyChanged(nameof(ShowRecurringMonthlyInput));
        OnPropertyChanged(nameof(ShowDateSelector));
        OnPropertyChanged(nameof(DateOrRecurrenceLabel));
        NotifyFormStateChanged();
    }
    partial void OnSelectedRecurringPeriodChanged(RecurringPeriod value)
    {
        RecurringTimeText = GetDefaultRecurringTimeText(value);
        OnPropertyChanged(nameof(ShowRecurringNoneInput));
        OnPropertyChanged(nameof(ShowRecurringWeekdayInput));
        OnPropertyChanged(nameof(ShowRecurringMonthlyInput));
        NotifyFormStateChanged();
    }
    partial void OnRecurringTimeTextChanged(string value) => NotifyFormStateChanged();
    partial void OnIsRecurringModeLockedChanged(bool value) => OnPropertyChanged(nameof(CanToggleRecurring));
    partial void OnIsPinnedChanged(bool value) => NotifyFormStateChanged();
    partial void OnIsMoreTagsOpenChanged(bool value) => NotifyFormStateChanged();
    partial void OnIsSavingChanged(bool value) => NotifyFormStateChanged();
    partial void OnNameTextChanged(string value)
    {
        RefreshActiveValidation(nameof(NameText));
        NotifyFormStateChanged();
        _ = RefreshTransactionNameSuggestionsAsync();
    }
    partial void OnNoteTextChanged(string value) => NotifyFormStateChanged();
    partial void OnSelectedDateChanged(DateTime value)
    {
        RefreshActiveValidation(nameof(AmountText));
        NotifyFormStateChanged();
    }
    partial void OnSelectedExpenseCategoryChanged(ExpenseCategory value) => NotifyFormStateChanged();
    partial void OnSelectedGoalChanged(SavingGoalVM? value) => NotifyFormStateChanged();
    partial void OnSelectedSpendingSourceChanged(SpendingSourceVM? value)
    {
        RefreshActiveValidation(nameof(AmountText));
        NotifyFormStateChanged();
    }

    public void ValidateNameField()
    {
        _isNameValidationActive = true;
        ValidateProperty(NameText, nameof(NameText));
    }

    public void ValidateAmountField()
    {
        _isAmountValidationActive = true;
        ValidateProperty(AmountText, nameof(AmountText));
    }

    public void ActivateAmountValidation()
    {
        if (_isAmountValidationActive)
            return;

        ValidateAmountField();
    }

    public void InitializeFromDraft(AddNewTransactionDraft draft)
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
        OnPropertyChanged(nameof(CanUseHistory));

        if (!value || IsGoal)
            IsMoreTagsOpen = false;

        RefreshSpendingSources();
        ClearNameValidation();
        RefreshActiveValidation(nameof(AmountText));
        _ = RefreshTransactionNameSuggestionsAsync();
        ResetHistoryLists();
        if (IsHistoryOpen)
            _ = LoadHistoryAsync();
        NotifyFormStateChanged();
    }

    partial void OnIsGoalChanged(bool value)
    {
        if (value && IsExpense)
            IsExpense = false;

        OnPropertyChanged(nameof(IsIncome));
        OnPropertyChanged(nameof(CanUseHistory));

        if (value)
        {
            IsMoreTagsOpen = false;
            IsHistoryOpen = false;
        }

        RefreshSpendingSources();
        ClearNameValidation();
        RefreshActiveValidation(nameof(AmountText));
        _ = RefreshTransactionNameSuggestionsAsync();
        ResetHistoryLists();
        if (IsHistoryOpen)
            _ = LoadHistoryAsync();
        NotifyFormStateChanged();
    }

    partial void OnSelectedPinnedHistoryItemChanged(AddNewTransactionHistoryItemVM? value)
    {
        if (value is null)
            return;

        SelectedHistoryItem = null;
        ApplyHistoryItem(value);
    }

    partial void OnSelectedHistoryItemChanged(AddNewTransactionHistoryItemVM? value)
    {
        if (value is null)
            return;

        SelectedPinnedHistoryItem = null;
        ApplyHistoryItem(value);
    }

    public void ApplyTransactionNameSuggestion(AddNewTransactionSuggestion suggestion)
    {
        NameText = suggestion.Name;
        AmountText = suggestion.Amount;
        NoteText = suggestion.Note;
        SelectedSpendingSource = SpendingSources.FirstOrDefault(source => source.Id == suggestion.SpendingSourceId) ??
                                 SelectedSpendingSource;

        if (IsExpense)
        {
            SelectedExpenseCategory = suggestion.Category ?? SelectedExpenseCategory;
            SelectedTag = suggestion.TagId is int tagId
                ? _orderedTags.FirstOrDefault(tag => tag.Id == tagId) ?? SelectedTag
                : SelectedTag;
        }

        ClearTransactionNameSuggestions();
        NotifyFormStateChanged();
    }

    partial void OnSelectedTagChanged(ExpenseTagVM? value)
    {
        RefreshActiveValidation(nameof(AmountText));
        NotifyFormStateChanged();

        if (_isUpdatingTagCollections || value is null)
            return;

        if (OverflowTags.Any(tag => tag.Id == value.Id))
            PromoteTagToVisibleStart(value);

        IsMoreTagsOpen = false;
    }

    public async Task<AddNewTransactionSubmissionResult> SaveAsync(bool resetAfterSave)
    {
        if (IsSaving)
            return AddNewTransactionSubmissionResult.Failure("A transaction is already being saved.");

        if (!TryBuildTransactionInput(out var input, out var validationMessage))
            return AddNewTransactionSubmissionResult.Failure(validationMessage);

        IsSaving = true;

        try
        {
            if (input.IsRecurring && _saveRecurringDraftAsync is not null)
            {
                if (!TryNormalizeRecurringTime(input.RecurringPeriod, input.RecurringTimeText, out var recurringTime))
                    return AddNewTransactionSubmissionResult.Failure(GetRecurringTimeValidationMessage(input.RecurringPeriod));

                var recurringType = input.IsGoal
                    ? RecurringTransactionType.GoalUpdate
                    : input.IsExpense
                        ? RecurringTransactionType.Expense
                        : RecurringTransactionType.Income;

                var recurringName = input.IsGoal && input.GoalId is not null
                    ? BuildGoalUpdateName((await _appData.GetSavingGoalByIdAsync(input.GoalId.Value))?.Name ?? string.Empty)
                    : BuildExpenseName(input.Name, input.Note, input.IsExpense ? "Recurring Expense" : "Recurring Income");

                var draftSaveResult = await _saveRecurringDraftAsync(new RecurringDraftSaveInput(
                    input.EditingRecurringTransactionId,
                    recurringType,
                    recurringName,
                    input.Amount,
                    input.RecurringPeriod,
                    recurringTime,
                    input.SpendingSourceId,
                    input.TagId,
                    input.GoalId));
                if (!draftSaveResult.IsSuccess)
                    return draftSaveResult;

                if (resetAfterSave)
                {
                    ReloadChoicesFromMainViewModel();
                    ResetForm(true);
                }

                return AddNewTransactionSubmissionResult.Success();
            }

            var spendingSource = await _appData.GetSpendingSourceByIdAsync(input.SpendingSourceId);
            if (spendingSource is null)
                return AddNewTransactionSubmissionResult.Failure("Please select a valid spending source.");

            if (!TryValidateSpendingAmountAgainstSource(input.IsExpense, input.IsGoal, input.Amount, spendingSource, out var spendingValidationMessage))
                return AddNewTransactionSubmissionResult.Failure(spendingValidationMessage);

            var invalidationScope = DashboardDataInvalidationScope.Budget | DashboardDataInvalidationScope.Notifications;

            if (input.IsRecurring)
            {
                if (!TryNormalizeRecurringTime(input.RecurringPeriod, input.RecurringTimeText, out var recurringTime))
                    return AddNewTransactionSubmissionResult.Failure(GetRecurringTimeValidationMessage(input.RecurringPeriod));

                var recurringType = input.IsGoal
                    ? RecurringTransactionType.GoalUpdate
                    : input.IsExpense
                        ? RecurringTransactionType.Expense
                        : RecurringTransactionType.Income;

                RecurringTransaction recurring;
                if (input.EditingRecurringTransactionId is > 0)
                {
                    recurring = await _appData.GetRecurringTransactionByIdAsync(input.EditingRecurringTransactionId.Value) ?? new RecurringTransaction();
                }
                else
                {
                    recurring = new RecurringTransaction();
                }

                recurring.Name = input.IsGoal && input.GoalId is not null
                    ? BuildGoalUpdateName((await _appData.GetSavingGoalByIdAsync(input.GoalId.Value))?.Name ?? string.Empty)
                    : BuildExpenseName(input.Name, input.Note, input.IsExpense ? "Recurring Expense" : "Recurring Income");
                recurring.Amount = input.Amount;
                recurring.RecurringPeriod = input.RecurringPeriod;
                recurring.RecurringTime = recurringTime;
                recurring.Type = recurringType;
                recurring.SourceId = input.SpendingSourceId;
                recurring.TagId = input.IsExpense ? input.TagId : null;
                recurring.GoalId = input.IsGoal ? input.GoalId : null;
                recurring.IsEnabled = true;

                if (input.EditingRecurringTransactionId is > 0)
                    _appData.UpdateRecurringTransaction(recurring);
                else
                    await _appData.AddRecurringTransactionAsync(recurring);

                await _appData.SaveChangesAsync();
            }
            else if (input.IsGoal)
            {
                if (input.GoalId is null)
                    return AddNewTransactionSubmissionResult.Failure("Please choose a goal.");

                if (!GoalUpdateTransactionSupport.IsEligibleGoalSourceType(spendingSource.SpendingSourceType))
                    return AddNewTransactionSubmissionResult.Failure("Goal updates can only be taken from Cash or Checking.");

                var goal = await _appData.GetSavingGoalByIdAsync(input.GoalId.Value);
                if (goal is null)
                    return AddNewTransactionSubmissionResult.Failure("Please select a valid goal.");

                var budgetPolicyResult = await ApplyExpenseBudgetPolicyAsync(
                    ExpenseCategory.Savings,
                    input.Amount,
                    input.Date);
                if (!budgetPolicyResult.IsSuccess)
                    return budgetPolicyResult;

                var goalUpdateTag = await GoalUpdateTransactionSupport.ResolveGoalUpdateTagAsync(_appData);
                var expense = new Expense
                {
                    Name = BuildGoalUpdateName(goal.Name),
                    Amount = input.Amount,
                    ExpenseCategory = ExpenseCategory.Savings,
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
                    SpendingSourceId = spendingSource.Id,
                    IsPinned = false
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
                        expense.ExpenseCategory,
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
                    return AddNewTransactionSubmissionResult.Failure("Please select a valid tag.");

                var budgetPolicyResult = await ApplyExpenseBudgetPolicyAsync(input.Category!.Value, input.Amount, input.Date);
                if (!budgetPolicyResult.IsSuccess)
                    return budgetPolicyResult;

                var expense = new Expense
                {
                    Name = BuildExpenseName(input.Name, input.Note, expenseTag.Name),
                    Amount = input.Amount,
                    ExpenseCategory = input.Category!.Value,
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
                    SpendingSourceId = spendingSource.Id,
                    IsPinned = input.IsPinned
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
                        expense.ExpenseCategory,
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
                    Name = input.Name,
                    Amount = input.Amount,
                    AddedOn = input.Date,
                    Notes = input.Note,
                    SpendingSourceId = spendingSource.Id,
                    IsPinned = input.IsPinned
                };

                await _appData.AddIncomeLogAsync(incomeLog);

                ApplyIncomeToSpendingSource(spendingSource, input.Amount);
                _appData.UpdateSpendingSource(spendingSource);

                await _appData.SaveChangesAsync();
                WeakReferenceMessenger.Default.Send(
                    new RecordLogMemoryMessage(new AddIncomeLogMemoryAction(new IncomeLogMemorySnapshot(
                        incomeLog.Id,
                        spendingSource.Id,
                        incomeLog.Name,
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

            return AddNewTransactionSubmissionResult.Success();
        }
        catch (Exception exception)
        {
            FluxoLogManager.LogError(exception, "Unable to save quick-add transaction.");
            return AddNewTransactionSubmissionResult.Failure(FluxoLogManager.CreateFailureMessage("save transaction"));
        }
        finally
        {
            IsSaving = false;
        }
    }

    public async Task<bool> HasSimilarTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (!TryBuildTransactionInput(out var input, out _) || input.IsRecurring)
            return false;

        try
        {
            var candidateName = await ResolveSimilarTransactionNameAsync(input, cancellationToken);
            if (string.IsNullOrWhiteSpace(candidateName))
                return false;

            if (!input.IsExpense && !input.IsGoal)
            {
                var incomeLogs = await _appData.GetIncomeLogsAsync(cancellationToken);
                return incomeLogs.Any(log => IsSimilarIncomeTransaction(log, input, candidateName));
            }

            var expenseLogs = await _appData.GetExpenseLogsAsync(cancellationToken);
            var goalUpdateTagIds = await GetGoalUpdateTagIdsAsync(cancellationToken);
            return expenseLogs.Any(log => IsSimilarExpenseTransaction(
                log,
                input,
                candidateName,
                input.IsGoal,
                goalUpdateTagIds));
        }
        catch (Exception exception)
        {
            FluxoLogManager.LogError(exception, "Unable to check for similar quick-add transaction.");
            return false;
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
        IsRecurring = false;
        IsPinned = false;
        IsRecurringModeLocked = false;
        SelectedRecurringPeriod = RecurringPeriod.Monthly;
        RecurringTimeText = GetDefaultRecurringTimeText(SelectedRecurringPeriod);
        SelectedExpenseCategory = ExpenseCategory.Needs;
        SelectedSpendingSource = SpendingSources.FirstOrDefault();
        SelectedTag = _orderedTags.FirstOrDefault();
        SelectedGoal = Goals.FirstOrDefault();
        IsMoreTagsOpen = false;
        ClearTransactionNameSuggestions();
    }

    public void SetVisibleTagSlots(int visibleTagSlots)
    {
        var normalizedSlots = Math.Max(0, visibleTagSlots);
        if (_visibleTagSlots == normalizedSlots)
            return;

        _visibleTagSlots = normalizedSlots;
        RefreshTagCollections();
    }

    private bool TryBuildTransactionInput(out QuickTransactionInput input, out string validationMessage)
    {
        input = default;
        validationMessage = string.Empty;

        _isNameValidationActive = true;
        _isAmountValidationActive = true;
        ValidateAllProperties();
        if (HasErrors)
        {
            validationMessage = GetFirstValidationMessage();
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

        if (SelectedSpendingSource is null)
        {
            validationMessage = "Please choose a spending source.";
            return false;
        }

        input = new QuickTransactionInput(
            IsExpense,
            IsGoal,
            IsRecurring,
            IsPinned,
            _editingRecurringTransactionId,
            SelectedRecurringPeriod,
            NameText.Trim(),
            AmountText,
            SelectedSpendingSource.Id,
            SelectedDate.Date,
            RecurringTimeText.Trim(),
            NoteText.Trim(),
            category,
            tagId,
            goalId);

        return true;
    }

    private void ReloadChoicesFromMainViewModel()
    {
        _availableSpendingSources.Clear();
        var sourceCatalog = _spendingSourcesOverride ?? _mainViewModel.BudgetPanel.SpendingSources;
        _availableSpendingSources.AddRange(sourceCatalog.Where(source => source.IsEnabled));

        _orderedTags.Clear();
        _orderedTags.AddRange(OrderNonSystemTags(_mainViewModel.BudgetPanel.Tags
            .Concat(_mainViewModel.BudgetPanel.OtherTags)
            .GroupBy(tag => tag.Id)
            .Select(group => group.First())));

        _orderedGoals.Clear();
        _orderedGoals.AddRange(_mainViewModel.SavingGoalsPanel.SavingGoals
            .GroupBy(goal => goal.Id)
            .Select(group => group.First())
            .OrderBy(goal => goal.Name));

        ReplaceCollection(Goals, _orderedGoals);
        RefreshTagCollections();
        RefreshSpendingSources();
        _ = RefreshExpenseCategoryAvailabilityAsync();
    }

    private async Task RefreshExpenseCategoryAvailabilityAsync()
    {
        try
        {
            var allocation = await _appData.GetBudgetAllocationAsync();
            if (allocation.OverspendPolicy != OverspendPolicy.HardStop)
            {
                SetAllExpenseCategoriesEnabled(true);
                return;
            }

            var snapshot = await BuildBudgetAllocationSnapshotAsync(allocation, DateTime.Today);
            foreach (var option in ExpenseCategories)
                option.IsEnabled = GetCategoryState(snapshot, option.Value).Remaining > 0m;
        }
        catch
        {
            SetAllExpenseCategoriesEnabled(true);
        }
    }

    private async Task<AddNewTransactionSubmissionResult> ApplyExpenseBudgetPolicyAsync(
        ExpenseCategory category,
        decimal amount,
        DateTime expenseDate)
    {
        var allocation = await _appData.GetBudgetAllocationAsync();
        if (allocation.OverspendPolicy == OverspendPolicy.Ignore)
            return AddNewTransactionSubmissionResult.Success();

        var snapshot = await BuildBudgetAllocationSnapshotAsync(allocation, expenseDate);
        var categoryState = GetCategoryState(snapshot, category);

        if (allocation.OverspendPolicy == OverspendPolicy.HardStop &&
            BudgetAllocationCalculator.WouldHardStop(categoryState, amount))
        {
            return AddNewTransactionSubmissionResult.Failure(
                $"{GetExpenseCategoryLabel(category)} budget is exhausted for this allocation period.");
        }

        if (allocation.OverspendPolicy == OverspendPolicy.SoftDebt)
        {
            var debtDelta = BudgetAllocationCalculator.CalculateSoftDebtDelta(categoryState.Remaining, amount);
            if (debtDelta > 0m)
            {
                AddDebtDelta(allocation, category, debtDelta);
                _appData.UpdateBudgetAllocation(allocation);
            }
        }

        return AddNewTransactionSubmissionResult.Success();
    }

    private async Task<BudgetAllocationSnapshot> BuildBudgetAllocationSnapshotAsync(
        BudgetAllocation allocation,
        DateTime allocationDate)
    {
        var expenseLogs = await _appData.GetExpenseLogsAsync();
        var currentPeriod = BudgetAllocationCalculator.ResolveCurrentPeriod(
            allocation.AllocationPeriod,
            allocationDate,
            allocation.PeriodStart);
        var previousPeriod = BudgetAllocationCalculator.ResolvePreviousPeriod(
            allocation.AllocationPeriod,
            allocationDate,
            allocation.PeriodStart);

        return BudgetAllocationCalculator.CalculateSnapshot(
            allocation,
            CalculateSpentByCategory(expenseLogs, currentPeriod),
            CalculateSpentByCategory(expenseLogs, previousPeriod),
            allocationDate,
            _mainViewModel.BudgetPanel.TotalIncomeAmount);
    }

    private static IReadOnlyDictionary<ExpenseCategory, decimal> CalculateSpentByCategory(
        IEnumerable<ExpenseLog> expenseLogs,
        BudgetAllocationPeriod period)
    {
        return expenseLogs
            .Where(log => !log.IsForDeletion)
            .Where(log => log.DeductedOn.Date >= period.Start && log.DeductedOn.Date <= period.End)
            .Where(log => log.Expense is not null)
            .GroupBy(log => log.Expense!.ExpenseCategory)
            .ToDictionary(group => group.Key, group => group.Sum(log => log.Amount));
    }

    private static BudgetAllocationCategoryState GetCategoryState(
        BudgetAllocationSnapshot snapshot,
        ExpenseCategory category)
    {
        return category switch
        {
            ExpenseCategory.Wants => snapshot.Wants,
            ExpenseCategory.Savings => snapshot.Invest,
            _ => snapshot.Needs
        };
    }

    private static void AddDebtDelta(BudgetAllocation allocation, ExpenseCategory category, decimal debtDelta)
    {
        switch (category)
        {
            case ExpenseCategory.Wants:
                allocation.WantsDebt += debtDelta;
                break;
            case ExpenseCategory.Savings:
                allocation.InvestDebt += debtDelta;
                break;
            default:
                allocation.NeedsDebt += debtDelta;
                break;
        }
    }

    private static string GetExpenseCategoryLabel(ExpenseCategory category)
    {
        return category switch
        {
            ExpenseCategory.Wants => "Wants",
            ExpenseCategory.Savings => "Invest",
            _ => "Needs"
        };
    }

    private void SetAllExpenseCategoriesEnabled(bool isEnabled)
    {
        foreach (var option in ExpenseCategories)
            option.IsEnabled = isEnabled;
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
            ReplaceCollection(VisibleTags, _orderedTags.Take(_visibleTagSlots));
            ReplaceCollection(OverflowTags, _orderedTags.Skip(_visibleTagSlots));

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

    private async Task<string> ResolveSimilarTransactionNameAsync(
        QuickTransactionInput input,
        CancellationToken cancellationToken)
    {
        if (input.IsGoal)
        {
            if (input.GoalId is null)
                return string.Empty;

            var goal = await _appData.GetSavingGoalByIdAsync(input.GoalId.Value, cancellationToken);
            return goal is null ? string.Empty : BuildGoalUpdateName(goal.Name);
        }

        return input.Name.Trim();
    }

    private async Task<HashSet<int>> GetGoalUpdateTagIdsAsync(CancellationToken cancellationToken)
    {
        var tags = await _appData.GetExpenseTagsAsync(cancellationToken);
        return tags
            .Where(tag => string.Equals(
                tag.Name?.Trim(),
                GoalUpdateTransactionSupport.GoalUpdateTagName,
                StringComparison.OrdinalIgnoreCase))
            .Select(tag => tag.Id)
            .ToHashSet();
    }

    private static bool IsSimilarExpenseTransaction(
        ExpenseLog log,
        QuickTransactionInput input,
        string candidateName,
        bool candidateIsGoalUpdate,
        IReadOnlySet<int> goalUpdateTagIds)
    {
        if (log.IsForDeletion ||
            log.SpendingSourceId != input.SpendingSourceId ||
            log.Expense is null ||
            !IsSameTransactionName(log.Expense.Name, candidateName) ||
            !IsSimilarAmount(log.Amount, input.Amount))
        {
            return false;
        }

        return IsGoalUpdateExpenseLog(log, goalUpdateTagIds) == candidateIsGoalUpdate;
    }

    private static bool IsSimilarIncomeTransaction(
        IncomeLog log,
        QuickTransactionInput input,
        string candidateName)
    {
        return log.SpendingSourceId == input.SpendingSourceId &&
               IsSameTransactionName(log.Name, candidateName) &&
               IsSimilarAmount(log.Amount, input.Amount);
    }

    private static bool IsGoalUpdateExpenseLog(ExpenseLog log, IReadOnlySet<int> goalUpdateTagIds)
    {
        var tagName = log.Expense?.ExpenseTag?.Name;
        if (!string.IsNullOrWhiteSpace(tagName))
            return string.Equals(
                tagName.Trim(),
                GoalUpdateTransactionSupport.GoalUpdateTagName,
                StringComparison.OrdinalIgnoreCase);

        if (log.Expense is not null && log.Expense.ExpenseTagId > 0 && goalUpdateTagIds.Count > 0)
            return goalUpdateTagIds.Contains(log.Expense.ExpenseTagId);

        var expenseName = log.Expense?.Name?.Trim();
        return string.Equals(expenseName, GoalUpdateTransactionSupport.GoalUpdateTagName, StringComparison.OrdinalIgnoreCase) ||
               expenseName?.StartsWith($"{GoalUpdateTransactionSupport.GoalUpdateTagName}:", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsSameTransactionName(string? existingName, string candidateName)
    {
        return string.Equals(
            existingName?.Trim(),
            candidateName.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsSimilarAmount(decimal existingAmount, decimal candidateAmount)
    {
        existingAmount = Math.Abs(existingAmount);
        candidateAmount = Math.Abs(candidateAmount);

        if (existingAmount <= 0m || candidateAmount <= 0m)
            return existingAmount == candidateAmount;

        var lowerAmount = Math.Min(existingAmount, candidateAmount);
        var higherAmount = Math.Max(existingAmount, candidateAmount);
        return (higherAmount - lowerAmount) / lowerAmount <= SimilarAmountTolerance;
    }

    private async Task RefreshTransactionNameSuggestionsAsync()
    {
        var requestVersion = ++_transactionNameSuggestionRequestVersion;
        var query = NameText?.Trim() ?? string.Empty;
        if (query.Length < 3 || IsGoal)
        {
            ClearTransactionNameSuggestions();
            return;
        }

        try
        {
            var expenseLogsTask = IsExpense
                ? _appData.GetExpenseLogsAsync()
                : Task.FromResult<IReadOnlyList<ExpenseLog>>([]);
            var incomeLogsTask = IsIncome
                ? _appData.GetIncomeLogsAsync()
                : Task.FromResult<IReadOnlyList<IncomeLog>>([]);

            await Task.WhenAll(expenseLogsTask, incomeLogsTask);

            if (requestVersion != _transactionNameSuggestionRequestVersion)
                return;

            var suggestions = BuildTransactionNameSuggestions(
                expenseLogsTask.Result,
                incomeLogsTask.Result,
                IsExpense,
                query);
            ReplaceCollection(TransactionNameSuggestions, suggestions);
            OnPropertyChanged(nameof(HasTransactionNameSuggestions));
        }
        catch
        {
            if (requestVersion == _transactionNameSuggestionRequestVersion)
                ClearTransactionNameSuggestions();
        }
    }

    private void ClearTransactionNameSuggestions()
    {
        _transactionNameSuggestionRequestVersion++;

        if (TransactionNameSuggestions.Count == 0)
        {
            OnPropertyChanged(nameof(HasTransactionNameSuggestions));
            return;
        }

        TransactionNameSuggestions.Clear();
        OnPropertyChanged(nameof(HasTransactionNameSuggestions));
    }

    internal static IEnumerable<AddNewTransactionSuggestion> BuildTransactionNameSuggestions(
        IEnumerable<ExpenseLog> expenseLogs,
        IEnumerable<IncomeLog> incomeLogs,
        bool isExpense,
        string query)
    {
        var normalizedQuery = query.Trim();
        if (normalizedQuery.Length < 3)
            return [];

        return isExpense
            ? BuildExpenseTransactionNameSuggestions(expenseLogs, normalizedQuery)
            : BuildIncomeTransactionNameSuggestions(incomeLogs, normalizedQuery);
    }

    private static IEnumerable<AddNewTransactionSuggestion> BuildExpenseTransactionNameSuggestions(
        IEnumerable<ExpenseLog> expenseLogs,
        string query)
    {
        return expenseLogs
            .Where(log => !log.IsForDeletion)
            .Where(log => !string.IsNullOrWhiteSpace(log.Expense?.Name))
            .Where(log => log.Expense.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(log => log.DeductedOn)
            .Select(log => new AddNewTransactionSuggestion(
                log.Expense.Name,
                log.Amount,
                log.SpendingSourceId,
                log.SpendingSource?.Name ?? log.Expense.SpendingSource?.Name ?? string.Empty,
                log.Notes,
                log.Expense.ExpenseCategory,
                log.Expense.ExpenseTagId,
                null));
    }

    private static IEnumerable<AddNewTransactionSuggestion> BuildIncomeTransactionNameSuggestions(
        IEnumerable<IncomeLog> incomeLogs,
        string query)
    {
        return incomeLogs
            .Where(log => !string.IsNullOrWhiteSpace(log.Name))
            .Where(log => log.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(log => log.AddedOn)
            .Select(log => new AddNewTransactionSuggestion(
                log.Name,
                log.Amount,
                log.SpendingSourceId,
                log.SpendingSource?.Name ?? string.Empty,
                log.Notes,
                null,
                null,
                null));
    }

    private static bool TryValidateSpendingAmountAgainstSource(
        bool isExpense,
        bool isGoal,
        decimal amount,
        SpendingSourceVM source,
        out string validationMessage)
    {
        if (amount <= 0m)
        {
            validationMessage = "Please enter a valid amount greater than zero.";
            return false;
        }

        if (!isExpense && !isGoal)
        {
            validationMessage = string.Empty;
            return true;
        }

        if (!TryValidateMaximumSpending(source.MaximumSpending, source.SpendingSourceType, source.SpentAmount, source.MoneyOut, amount, out validationMessage))
            return false;

        return TryValidateSpendingCapacity(source.SpendingSourceType, source.Balance, source.AccountLimit, source.SpentAmount, amount, out validationMessage);
    }

    private static bool TryValidateSpendingAmountAgainstSource(
        bool isExpense,
        bool isGoal,
        decimal amount,
        SpendingSource source,
        out string validationMessage)
    {
        if (amount <= 0m)
        {
            validationMessage = "Please enter a valid amount greater than zero.";
            return false;
        }

        if (!isExpense && !isGoal)
        {
            validationMessage = string.Empty;
            return true;
        }

        var persistedMoneyOut = GetPersistedMoneyOut(source);
        if (!TryValidateMaximumSpending(source.MaximumSpending, source.SpendingSourceType, source.SpentAmount, persistedMoneyOut, amount, out validationMessage))
            return false;

        return TryValidateSpendingCapacity(source.SpendingSourceType, source.Balance, source.AccountLimit, source.SpentAmount, amount, out validationMessage);
    }

    private static decimal GetPersistedMoneyOut(SpendingSource source)
    {
        var moneyOutProperty = source.GetType().GetProperty("MoneyOut");
        if (moneyOutProperty is not null)
        {
            var rawValue = moneyOutProperty.GetValue(source);
            if (rawValue is decimal moneyOut)
                return moneyOut;
        }

        return source.SpentAmount;
    }

    private static bool TryValidateMaximumSpending(
        decimal maximumSpending,
        SpendingSourceType sourceType,
        decimal spentAmount,
        decimal moneyOut,
        decimal amount,
        out string validationMessage)
    {
        if (maximumSpending <= 0m)
        {
            validationMessage = string.Empty;
            return true;
        }

        var projectedSpending = sourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL
            ? spentAmount + amount
            : moneyOut + amount;

        if (projectedSpending <= maximumSpending)
        {
            validationMessage = string.Empty;
            return true;
        }

        validationMessage = "Amount exceeds this source's maximum spending limit.";
        return false;
    }

    private static bool TryValidateSpendingCapacity(
        SpendingSourceType sourceType,
        decimal balance,
        decimal accountLimit,
        decimal spentAmount,
        decimal amount,
        out string validationMessage)
    {
        if (sourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL)
        {
            if (spentAmount + amount <= accountLimit)
            {
                validationMessage = string.Empty;
                return true;
            }

            validationMessage = "Amount exceeds this source's account limit.";
            return false;
        }

        if (amount <= balance)
        {
            validationMessage = string.Empty;
            return true;
        }

        validationMessage = "Amount exceeds this source's available balance.";
        return false;
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

    private string GetFirstValidationMessage()
    {
        foreach (var propertyName in new[]
                 {
                     nameof(NameText),
                     nameof(AmountText),
                     nameof(SelectedSpendingSource),
                     nameof(SelectedGoal),
                     nameof(SelectedTag),
                     nameof(RecurringTimeText)
                 })
        {
            var propertyMessage = GetErrors(propertyName)
                .OfType<ValidationResult>()
                .Select(result => result.ErrorMessage)
                .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message));

            if (!string.IsNullOrWhiteSpace(propertyMessage))
                return propertyMessage;
        }

        var fallbackMessage = GetErrors()
            .OfType<ValidationResult>()
            .Select(result => result.ErrorMessage)
            .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message));

        return fallbackMessage ?? "Please fix the highlighted fields.";
    }

    private FormState CaptureState()
    {
        return new FormState(
            IsExpense,
            IsGoal,
            IsRecurring,
            IsPinned,
            SelectedRecurringPeriod,
            NameText ?? string.Empty,
            AmountText,
            RecurringTimeText ?? string.Empty,
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

    private bool HasPendingTransactionInputChanges()
    {
        var initialInput = new PendingTransactionInputState(
            _initialState.NameText ?? string.Empty,
            _initialState.AmountText);
        var currentInput = new PendingTransactionInputState(
            NameText ?? string.Empty,
            AmountText);

        return HasPendingTransactionInputValue(currentInput) && !currentInput.Equals(initialInput);
    }

    private static bool HasPendingTransactionInputValue(PendingTransactionInputState state)
    {
        return !string.IsNullOrWhiteSpace(state.NameText) || state.AmountText > 0m;
    }

    [RelayCommand]
    public async Task LoadHistoryAsync(CancellationToken cancellationToken = default)
    {
        if (!CanUseHistory)
        {
            ResetHistoryLists();
            return;
        }

        try
        {
            if (IsExpense)
            {
                var expenseLogs = await _appData.GetExpenseLogsAsync(cancellationToken);
                PinnedHistory.Reset(AddNewTransactionHistoryBuilder.BuildPinnedExpenses(expenseLogs));
                TransactionHistory.Reset(AddNewTransactionHistoryBuilder.BuildExpenseHistory(expenseLogs));
                return;
            }

            var incomeLogs = await _appData.GetIncomeLogsAsync(cancellationToken);
            PinnedHistory.Reset(AddNewTransactionHistoryBuilder.BuildPinnedIncomes(incomeLogs));
            TransactionHistory.Reset(AddNewTransactionHistoryBuilder.BuildIncomeHistory(incomeLogs));
        }
        catch (Exception exception)
        {
            FluxoLogManager.LogError(exception, "Unable to load add transaction history.");
            ResetHistoryLists();
        }
    }

    private void ResetHistoryLists()
    {
        SelectedPinnedHistoryItem = null;
        SelectedHistoryItem = null;
        PinnedHistory.Reset([]);
        TransactionHistory.Reset([]);
    }

    private void ApplyHistoryItem(AddNewTransactionHistoryItemVM item)
    {
        IsExpense = item.IsExpense;
        IsGoal = false;
        NameText = item.Name;
        AmountText = item.Amount;
        NoteText = item.Note;
        SelectedDate = item.Date.Date;
        IsPinned = item.IsPinned;
        SelectedSpendingSource = SpendingSources.FirstOrDefault(source => source.Id == item.SpendingSourceId) ??
                                 SelectedSpendingSource;

        if (item.IsExpense)
        {
            SelectedExpenseCategory = item.Category ?? SelectedExpenseCategory;
            SelectedTag = item.TagId is int tagId
                ? _orderedTags.FirstOrDefault(tag => tag.Id == tagId) ?? SelectedTag
                : SelectedTag;
        }

        ClearTransactionNameSuggestions();
        NotifyFormStateChanged();
    }

    private bool IsCurrentInputValid()
    {
        return IsValidationSuccess(ValidateNameText(NameText, CreateValidationContext()))
               && IsValidationSuccess(ValidateAmountText(AmountText, CreateValidationContext()))
               && IsValidationSuccess(ValidateSelectedSpendingSource(SelectedSpendingSource, CreateValidationContext()))
               && IsValidationSuccess(ValidateSelectedTag(SelectedTag, CreateValidationContext()))
               && IsValidationSuccess(ValidateSelectedGoal(SelectedGoal, CreateValidationContext()))
               && IsValidationSuccess(ValidateRecurringTimeText(RecurringTimeText, CreateValidationContext()));
    }

    private ValidationContext CreateValidationContext()
    {
        return new ValidationContext(this);
    }

    private static bool IsValidationSuccess(ValidationResult? result)
    {
        return result is null || result == ValidationResult.Success;
    }

    private void RefreshActiveValidation(params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (propertyName == nameof(NameText) && _isNameValidationActive)
                ValidateProperty(NameText, nameof(NameText));

            if (propertyName == nameof(AmountText) && _isAmountValidationActive)
                ValidateProperty(AmountText, nameof(AmountText));
        }
    }

    private void ClearNameValidation()
    {
        _isNameValidationActive = false;
        ClearErrors(nameof(NameText));
        OnPropertyChanged(nameof(NameValidationHint));
        OnPropertyChanged(nameof(CanSave));
    }

    private string GetValidationHint(string propertyName)
    {
        var message = GetErrors(propertyName)
            .OfType<ValidationResult>()
            .Select(result => result.ErrorMessage)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        if (string.IsNullOrWhiteSpace(message))
            return string.Empty;

        return propertyName switch
        {
            nameof(NameText) => GetNameValidationHint(message),
            nameof(AmountText) => GetAmountValidationHint(message),
            _ => string.Empty
        };
    }

    private static string GetNameValidationHint(string message)
    {
        if (message.Contains("enter a name", StringComparison.OrdinalIgnoreCase))
            return "Required";

        if (message.Contains("exceed", StringComparison.OrdinalIgnoreCase))
            return "Too Long";

        return "Invalid Name";
    }

    private static string GetAmountValidationHint(string message)
    {
        if (message.Contains("available balance", StringComparison.OrdinalIgnoreCase))
            return "Insufficient Balance";

        if (message.Contains("maximum spending", StringComparison.OrdinalIgnoreCase))
            return "Overflow Balance";

        if (message.Contains("account limit", StringComparison.OrdinalIgnoreCase))
            return "Account Limit";

        if (message.Contains("spending limit", StringComparison.OrdinalIgnoreCase))
            return "Tag Limit";

        return "Invalid Amount";
    }

    private void RefreshSpendingSources()
    {
        var selectedSpendingSourceId = SelectedSpendingSource?.Id;

        var filteredSources = _availableSpendingSources
            .Where(source =>
                IsGoal
                    ? GoalUpdateTransactionSupport.IsEligibleGoalSourceType(source.SpendingSourceType)
                    : IsExpense || source.SpendingSourceType is not (SpendingSourceType.Credit or SpendingSourceType.BNPL))
            .OrderBy(GetSpendingSourceTypeSortOrder)
            .ThenByDescending(GetSpendingSourceWithinTypeSortValue)
            .ThenBy(source => source.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        ReplaceCollection(SpendingSources, filteredSources);

        SelectedSpendingSource = selectedSpendingSourceId is null
            ? SpendingSources.FirstOrDefault()
            : SpendingSources.FirstOrDefault(source => source.Id == selectedSpendingSourceId.Value) ??
              SpendingSources.FirstOrDefault();
    }

    public static ValidationResult? ValidateNameText(string value, ValidationContext validationContext)
    {
        var viewModel = (AddNewTransactionVM)validationContext.ObjectInstance;
        if (viewModel.IsGoal)
            return ValidationResult.Success;

        var trimmedName = value?.Trim() ?? string.Empty;

        if (trimmedName.Length == 0)
            return new ValidationResult("Please enter a name.");

        if (trimmedName.Length > MaxNameLength)
            return new ValidationResult($"Name cannot exceed {MaxNameLength} characters.");

        if (trimmedName.Any(char.IsControl))
            return new ValidationResult("Name cannot contain control characters.");

        return ValidationResult.Success;
    }

    public static ValidationResult? ValidateAmountText(decimal value, ValidationContext validationContext)
    {
        var viewModel = (AddNewTransactionVM)validationContext.ObjectInstance;
        if (value <= 0m)
            return new ValidationResult("Please enter a valid amount greater than zero.");

        if (viewModel.SelectedSpendingSource is null)
            return ValidationResult.Success;

        if (!TryValidateSpendingAmountAgainstSource(viewModel.IsExpense, viewModel.IsGoal, value, viewModel.SelectedSpendingSource, out var validationMessage))
            return new ValidationResult(validationMessage);

        if (!viewModel.TryValidateSpendingAmountAgainstTagLimit(value, out var tagLimitValidationMessage))
            return new ValidationResult(tagLimitValidationMessage);

        return ValidationResult.Success;
    }

    private bool TryValidateSpendingAmountAgainstTagLimit(decimal amount, out string validationMessage)
    {
        validationMessage = string.Empty;

        if (!IsExpense || IsRecurring || SelectedTag is not { SpendingLimit: > 0m } tag)
            return true;

        try
        {
            var allocation = _appData.GetBudgetAllocationAsync().GetAwaiter().GetResult();
            var currentPeriod = BudgetAllocationCalculator.ResolveCurrentPeriod(
                allocation.AllocationPeriod,
                SelectedDate.Date,
                allocation.PeriodStart);
            var currentTagSpending = _appData.GetExpenseLogsAsync().GetAwaiter().GetResult()
                .Where(log => !log.IsForDeletion)
                .Where(log => log.DeductedOn.Date >= currentPeriod.Start && log.DeductedOn.Date <= currentPeriod.End)
                .Where(log => log.Expense?.ExpenseTagId == tag.Id || log.Expense?.ExpenseTag?.Id == tag.Id)
                .Sum(log => log.Amount);

            if (currentTagSpending + amount <= tag.SpendingLimit.Value)
                return true;

            validationMessage = $"{tag.Name} spending limit exceeded.";
            return false;
        }
        catch
        {
            return true;
        }
    }

    public static ValidationResult? ValidateSelectedSpendingSource(SpendingSourceVM? value, ValidationContext validationContext)
    {
        _ = validationContext;
        return value is null
            ? new ValidationResult("Please choose a spending source.")
            : ValidationResult.Success;
    }

    public static ValidationResult? ValidateSelectedTag(ExpenseTagVM? value, ValidationContext validationContext)
    {
        var viewModel = (AddNewTransactionVM)validationContext.ObjectInstance;
        if (!viewModel.IsExpense)
            return ValidationResult.Success;

        return value is null
            ? new ValidationResult("Please choose a tag.")
            : ValidationResult.Success;
    }

    public static ValidationResult? ValidateSelectedGoal(SavingGoalVM? value, ValidationContext validationContext)
    {
        var viewModel = (AddNewTransactionVM)validationContext.ObjectInstance;
        if (!viewModel.IsGoal)
            return ValidationResult.Success;

        return value is null
            ? new ValidationResult("Please choose a goal.")
            : ValidationResult.Success;
    }

    public static ValidationResult? ValidateRecurringTimeText(string value, ValidationContext validationContext)
    {
        var viewModel = (AddNewTransactionVM)validationContext.ObjectInstance;
        if (!viewModel.IsRecurring || viewModel.SelectedRecurringPeriod == RecurringPeriod.None)
            return ValidationResult.Success;

        return TryNormalizeRecurringTime(viewModel.SelectedRecurringPeriod, value?.Trim() ?? string.Empty, out _)
            ? ValidationResult.Success
            : new ValidationResult(GetRecurringTimeValidationMessage(viewModel.SelectedRecurringPeriod));
    }

    internal static IEnumerable<ExpenseTagVM> ProjectNonSystemTags(IEnumerable<ExpenseTag> tags)
    {
        return tags
            .Where(tag => !tag.IsSystemTag)
            .OrderBy(tag => tag.Name, StringComparer.OrdinalIgnoreCase)
            .Select(tag => new ExpenseTagVM
            {
                Id = tag.Id,
                Name = tag.Name,
                HexCode = tag.HexCode,
                IsSystemTag = false,
                SpendingLimit = tag.SpendingLimit
            });
    }

    internal static IEnumerable<ExpenseTagVM> OrderNonSystemTags(IEnumerable<ExpenseTagVM> tags)
    {
        return tags
            .Where(tag => !tag.IsSystemTag)
            .OrderBy(tag => tag.Name, StringComparer.OrdinalIgnoreCase);
    }

    internal static int GetSpendingSourceTypeSortOrder(SpendingSourceVM source)
    {
        return source.SpendingSourceType switch
        {
            SpendingSourceType.Checking => 0,
            SpendingSourceType.Cash => 1,
            SpendingSourceType.Credit => 2,
            SpendingSourceType.BNPL => 3,
            SpendingSourceType.Saving => 4,
            _ => 5
        };
    }

    internal static decimal GetSpendingSourceWithinTypeSortValue(SpendingSourceVM source)
    {
        return source.SpendingSourceType switch
        {
            SpendingSourceType.Checking or SpendingSourceType.Cash => source.Balance,
            SpendingSourceType.Credit or SpendingSourceType.BNPL => source.AccountLimit - source.SpentAmount,
            SpendingSourceType.Saving => source.Balance,
            _ => source.Balance
        };
    }

    public sealed partial class ExpenseCategoryOption : ObservableObject
    {
        public ExpenseCategoryOption(string label, ExpenseCategory value)
        {
            Label = label;
            Value = value;
        }

        public string Label { get; }

        public ExpenseCategory Value { get; }

        [ObservableProperty]
        private bool _isEnabled = true;
    }

    public sealed record AddNewTransactionSuggestion(
        string Name,
        decimal Amount,
        int SpendingSourceId,
        string SpendingSourceName,
        string Note,
        ExpenseCategory? Category,
        int? TagId,
        DateTime? Date);

    public readonly record struct AddNewTransactionSubmissionResult(bool IsSuccess, string? ErrorMessage)
    {
        public static AddNewTransactionSubmissionResult Success()
        {
            return new AddNewTransactionSubmissionResult(true, null);
        }

        public static AddNewTransactionSubmissionResult Failure(string? errorMessage)
        {
            return new AddNewTransactionSubmissionResult(false, errorMessage);
        }
    }

    public readonly record struct AddNewTransactionDraft(
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

    public readonly record struct RecurringDraftSaveInput(
        int? EditingRecurringTransactionId,
        RecurringTransactionType Type,
        string Name,
        decimal Amount,
        RecurringPeriod RecurringPeriod,
        int RecurringTime,
        int SpendingSourceId,
        int? TagId,
        int? GoalId);

    public readonly record struct RecurringDraftSnapshot(
        int? EditingRecurringTransactionId,
        RecurringTransactionType Type,
        string Name,
        decimal Amount,
        RecurringPeriod RecurringPeriod,
        int RecurringTime,
        int SpendingSourceId,
        int? TagId,
        int? GoalId);

    private readonly record struct QuickTransactionInput(
        bool IsExpense,
        bool IsGoal,
        bool IsRecurring,
        bool IsPinned,
        int? EditingRecurringTransactionId,
        RecurringPeriod RecurringPeriod,
        string Name,
        decimal Amount,
        int SpendingSourceId,
        DateTime Date,
        string RecurringTimeText,
        string Note,
        ExpenseCategory? Category,
        int? TagId,
        int? GoalId);

    private readonly record struct FormState(
        bool IsExpense,
        bool IsGoal,
        bool IsRecurring,
        bool IsPinned,
        RecurringPeriod SelectedRecurringPeriod,
        string NameText,
        decimal AmountText,
        string RecurringTimeText,
        string NoteText,
        DateTime SelectedDate,
        ExpenseCategory SelectedExpenseCategory,
        int SelectedSpendingSourceId,
        int SelectedTagId,
        int SelectedGoalId);

    private readonly record struct PendingTransactionInputState(
        string NameText,
        decimal AmountText);

    private int? _editingRecurringTransactionId;

    public void InitializeRecurringMode(bool isLocked)
    {
        IsRecurringModeLocked = isLocked;
        IsRecurring = true;
    }

    public async Task<bool> InitializeFromRecurringTransactionAsync(int recurringTransactionId, CancellationToken cancellationToken = default)
    {
        var recurring = await _appData.GetRecurringTransactionByIdAsync(recurringTransactionId, cancellationToken);
        if (recurring is null)
            return false;

        _editingRecurringTransactionId = recurring.Id;
        IsRecurringModeLocked = true;
        IsRecurring = true;
        IsExpense = recurring.Type == RecurringTransactionType.Expense;
        IsGoal = recurring.Type == RecurringTransactionType.GoalUpdate;
        NameText = recurring.Name;
        AmountText = recurring.Amount;
        SelectedRecurringPeriod = recurring.RecurringPeriod;
        RecurringTimeText = recurring.RecurringPeriod == RecurringPeriod.None
            ? string.Empty
            : recurring.RecurringTime.ToString(CultureInfo.InvariantCulture);
        SelectedSpendingSource = SpendingSources.FirstOrDefault(source => source.Id == recurring.SourceId) ?? SpendingSources.FirstOrDefault();
        SelectedTag = recurring.TagId is > 0 ? _orderedTags.FirstOrDefault(tag => tag.Id == recurring.TagId.Value) : _orderedTags.FirstOrDefault();
        SelectedGoal = recurring.GoalId is > 0 ? Goals.FirstOrDefault(goal => goal.Id == recurring.GoalId.Value) : Goals.FirstOrDefault();
        return true;
    }

    public void InitializeFromRecurringDraft(RecurringDraftSnapshot draft)
    {
        _editingRecurringTransactionId = draft.EditingRecurringTransactionId;
        IsRecurringModeLocked = true;
        IsRecurring = true;
        IsExpense = draft.Type == RecurringTransactionType.Expense;
        IsGoal = draft.Type == RecurringTransactionType.GoalUpdate;
        NameText = draft.Name;
        AmountText = draft.Amount;
        SelectedRecurringPeriod = draft.RecurringPeriod;
        RecurringTimeText = draft.RecurringPeriod == RecurringPeriod.None
            ? string.Empty
            : draft.RecurringTime.ToString(CultureInfo.InvariantCulture);
        SelectedSpendingSource = SpendingSources.FirstOrDefault(source => source.Id == draft.SpendingSourceId) ?? SpendingSources.FirstOrDefault();
        SelectedTag = draft.TagId is > 0 ? _orderedTags.FirstOrDefault(tag => tag.Id == draft.TagId.Value) : _orderedTags.FirstOrDefault();
        SelectedGoal = draft.GoalId is > 0 ? Goals.FirstOrDefault(goal => goal.Id == draft.GoalId.Value) : Goals.FirstOrDefault();
    }

    internal static bool TryNormalizeRecurringTime(RecurringPeriod period, string text, out int recurringTime)
    {
        recurringTime = 0;
        if (period == RecurringPeriod.None)
            return true;

        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            return false;

        var max = IsWeekdayRecurringPeriod(period) ? 7 : MonthlyDueDateHelper.MaxMonthlyDay;
        if (parsed < 1 || parsed > max)
            return false;

        recurringTime = parsed;
        return true;
    }

    private static bool IsWeekdayRecurringPeriod(RecurringPeriod period)
    {
        return period is RecurringPeriod.Weekly or RecurringPeriod.Biweekly;
    }

    private static string GetDefaultRecurringTimeText(RecurringPeriod period)
    {
        if (period == RecurringPeriod.None)
            return string.Empty;

        if (IsWeekdayRecurringPeriod(period))
        {
            var day = DateTime.Today.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)DateTime.Today.DayOfWeek;
            return day.ToString(CultureInfo.InvariantCulture);
        }

        return MonthlyDueDateHelper.Normalize(DateTime.Today.Day)?.ToString(CultureInfo.InvariantCulture) ?? "1";
    }

    private static string GetRecurringTimeValidationMessage(RecurringPeriod period)
    {
        return IsWeekdayRecurringPeriod(period)
            ? "Recurring weekday must be between Monday and Sunday."
            : "Recurring day must be between 1 and 28.";
    }

    public sealed record RecurringTimeOption(string Label, string Value);
}
