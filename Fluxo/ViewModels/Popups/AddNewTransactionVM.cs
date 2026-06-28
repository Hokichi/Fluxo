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
using Fluxo.Resources.CustomControls;
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
    private const int NoAccountId = -1;
    private const int NoTagId = -1;
    private const int NoSavingGoalId = -1;
    private const decimal SimilarAmountTolerance = 0.05m;

    private readonly List<AccountVM> _availableAccounts = [];
    private readonly IReadOnlyList<AccountVM>? _accountsOverride;
    private readonly Func<RecurringDraftSaveInput, Task<AddNewTransactionSubmissionResult>>? _saveRecurringDraftAsync;
    private readonly List<SavingGoalVM> _orderedGoals = [];
    private readonly MainVM _mainViewModel;
    private readonly List<TagVM> _orderedTags = [];
    private readonly IAppDataService _appData;
    private FormState _initialState;
    private bool _isChangeTrackingInitialized;
    private bool _isAmountValidationActive;
    private bool _isNameValidationActive;
    private TransactionPopupPurpose _popupPurpose = TransactionPopupPurpose.AddNewTransaction;
    private bool _isTransactionTypeLocked;
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
    [ObservableProperty] private bool _isInstallments;
    [ObservableProperty] private DateTime _installmentEndDate = DateTime.Today;
    [ObservableProperty] private bool _isPinned;
    [ObservableProperty] private bool _isIoU;
    [ObservableProperty] private bool _isExcludedFromBudget;
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
    [CustomValidation(typeof(AddNewTransactionVM), nameof(ValidateSelectedAccount))]
    private AccountVM? _selectedAccount;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(AddNewTransactionVM), nameof(ValidateSelectedTag))]
    private TagVM? _selectedTag;

    private DateTime? _installmentCalculationDateOverride;

    internal DateTime InstallmentCalculationDate
    {
        get => _installmentCalculationDateOverride ?? DateTime.Today;
        set => _installmentCalculationDateOverride = value.Date;
    }

    public AddNewTransactionVM(
        MainVM mainViewModel,
        IAppDataService appData,
        IReadOnlyList<AccountVM>? accountsOverride = null,
        Func<RecurringDraftSaveInput, Task<AddNewTransactionSubmissionResult>>? saveRecurringDraftAsync = null)
    {
        _mainViewModel = mainViewModel;
        _appData = appData;
        _accountsOverride = accountsOverride;
        _saveRecurringDraftAsync = saveRecurringDraftAsync;
        ErrorsChanged += (_, e) =>
        {
            OnPropertyChanged(nameof(CanSave));

            if (e.PropertyName == nameof(NameText))
                OnPropertyChanged(nameof(NameValidationHint));

            if (e.PropertyName == nameof(AmountText))
                OnPropertyChanged(nameof(AmountValidationHint));
        };
        AccountsView = AccountComboBoxViewFactory.CreateGroupedByProperty(
            Accounts,
            nameof(AccountVM.TypeDisplayName));

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

    public ObservableCollection<AccountVM> Accounts { get; } = [];
    public ICollectionView AccountsView { get; }
    public ObservableCollection<SavingGoalVM> Goals { get; } = [];
    public ObservableCollection<TagVM> VisibleTags { get; } = [];
    public ObservableCollection<TagVM> OverflowTags { get; } = [];
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
    public bool IsRecurringTransactionMode => IsRecurring || IsInstallments;
    public bool ShowRecurringDayInput => IsRecurringTransactionMode;
    public bool ShowRecurringNoneInput => IsRecurringTransactionMode && SelectedRecurringPeriod == RecurringPeriod.None;
    public bool ShowRecurringWeekdayInput => IsRecurringTransactionMode && IsWeekdayRecurringPeriod(SelectedRecurringPeriod);
    public bool ShowRecurringMonthlyInput => IsRecurringTransactionMode && SelectedRecurringPeriod == RecurringPeriod.Monthly;
    public bool ShowDateSelector => !IsRecurringTransactionMode;
    public bool ShowInstallmentEndDate => IsInstallments;
    public bool CanUseInstallments => !IsGoal && CanToggleRecurring;
    public bool CanUseIoU => !IsGoal && CanToggleRecurring;
    public string DateOrRecurrenceLabel => IsRecurringTransactionMode ? "Recurrence" : "Date";
    public string InstallmentSummaryText => BuildInstallmentSummaryText();
    public bool CanToggleRecurring => !IsRecurringModeLocked;
    public bool CanUseHistory => true;
    public bool CanEditTransactionName => !IsGoal;
    public bool CanEditCategory => IsExpense;
    public bool CanEditTags => !IsGoal;
    public bool CanChangeTransactionType => !_isTransactionTypeLocked;
    public bool CanPinTransaction => _popupPurpose == TransactionPopupPurpose.AddNewTransaction && !IsRecurringTransactionMode;
    public string IoUTooltip => IsExpense ? "Set as lend" : "Set as debt";

    public string PopupTitle => _popupPurpose switch
    {
        TransactionPopupPurpose.AddRecurringTransaction => "Add Recurring Transaction",
        TransactionPopupPurpose.EditRecurringTransaction => "Edit Recurring Transaction",
        _ => "Add New Transaction"
    };

    public bool ShowNoteField => !IsGoal;
    public bool ShowGoalField => IsGoal;
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
        IReadOnlyList<Tag> allTags;
        try
        {
            allTags = await _appData.GetTagsAsync(cancellationToken);
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
        OnPropertyChanged(nameof(InstallmentSummaryText));
        NotifyFormStateChanged();
    }

    partial void OnIsRecurringChanged(bool value)
    {
        if (value && string.IsNullOrWhiteSpace(RecurringTimeText))
            RecurringTimeText = GetDefaultRecurringTimeText(SelectedRecurringPeriod);

        if (value && IsInstallments)
            IsInstallments = false;

        if (!CanPinTransaction)
            IsPinned = false;
        if (value && IsIoU)
            IsIoU = false;

        if (!CanUseIoU)
            IsIoU = false;

        OnPropertyChanged(nameof(ShowRecurringDayInput));
        OnPropertyChanged(nameof(ShowRecurringNoneInput));
        OnPropertyChanged(nameof(ShowRecurringWeekdayInput));
        OnPropertyChanged(nameof(ShowRecurringMonthlyInput));
        OnPropertyChanged(nameof(ShowDateSelector));
        OnPropertyChanged(nameof(IsRecurringTransactionMode));
        OnPropertyChanged(nameof(ShowInstallmentEndDate));
        OnPropertyChanged(nameof(DateOrRecurrenceLabel));
        OnPropertyChanged(nameof(InstallmentSummaryText));
        OnPropertyChanged(nameof(CanPinTransaction));
        OnPropertyChanged(nameof(CanUseIoU));
        RefreshActiveValidation(nameof(AmountText));
        NotifyFormStateChanged();
    }

    partial void OnIsInstallmentsChanged(bool value)
    {
        if (value)
        {
            IsRecurring = false;
            if (IsIoU)
                IsIoU = false;
        }

        if (!CanPinTransaction)
            IsPinned = false;
        if (!CanUseIoU)
            IsIoU = false;

        OnPropertyChanged(nameof(IsRecurringTransactionMode));
        OnPropertyChanged(nameof(ShowRecurringDayInput));
        OnPropertyChanged(nameof(ShowRecurringNoneInput));
        OnPropertyChanged(nameof(ShowRecurringWeekdayInput));
        OnPropertyChanged(nameof(ShowRecurringMonthlyInput));
        OnPropertyChanged(nameof(ShowDateSelector));
        OnPropertyChanged(nameof(DateOrRecurrenceLabel));
        OnPropertyChanged(nameof(ShowInstallmentEndDate));
        OnPropertyChanged(nameof(InstallmentSummaryText));
        OnPropertyChanged(nameof(CanPinTransaction));
        OnPropertyChanged(nameof(CanUseIoU));
        RefreshActiveValidation(nameof(AmountText));
        NotifyFormStateChanged();
    }

    partial void OnInstallmentEndDateChanged(DateTime value)
    {
        OnPropertyChanged(nameof(InstallmentSummaryText));
        RefreshActiveValidation(nameof(AmountText));
        NotifyFormStateChanged();
    }

    partial void OnSelectedRecurringPeriodChanged(RecurringPeriod value)
    {
        RecurringTimeText = GetDefaultRecurringTimeText(value);
        OnPropertyChanged(nameof(ShowRecurringNoneInput));
        OnPropertyChanged(nameof(ShowRecurringWeekdayInput));
        OnPropertyChanged(nameof(ShowRecurringMonthlyInput));
        OnPropertyChanged(nameof(InstallmentSummaryText));
        RefreshActiveValidation(nameof(AmountText));
        NotifyFormStateChanged();
    }

    partial void OnRecurringTimeTextChanged(string value)
    {
        OnPropertyChanged(nameof(InstallmentSummaryText));
        RefreshActiveValidation(nameof(AmountText));
        NotifyFormStateChanged();
    }

    partial void OnIsRecurringModeLockedChanged(bool value)
    {
        OnPropertyChanged(nameof(CanToggleRecurring));
        OnPropertyChanged(nameof(CanUseInstallments));
        OnPropertyChanged(nameof(CanUseIoU));
    }

    partial void OnIsIoUChanged(bool value)
    {
        if (value)
        {
            if (IsRecurring)
                IsRecurring = false;
            if (IsInstallments)
                IsInstallments = false;
        }

        NotifyFormStateChanged();
    }

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

    partial void OnSelectedGoalChanged(SavingGoalVM? value)
    {
        if (IsGoal)
            SyncGoalUpdateName();

        ResetHistoryLists();
        if (IsHistoryOpen)
            _ = LoadHistoryAsync();

        NotifyFormStateChanged();
    }

    partial void OnSelectedAccountChanged(AccountVM? value)
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

    [RelayCommand(CanExecute = nameof(CanToggleRecurring))]
    public void HandleRecurringModeClick()
    {
        if (!CanToggleRecurring)
            return;

        ClearTransactionModes();
        IsRecurring = true;
    }

    [RelayCommand(CanExecute = nameof(CanUseInstallments))]
    public void HandleInstallmentsModeClick()
    {
        if (!CanUseInstallments)
            return;

        ClearTransactionModes();
        IsInstallments = true;
    }

    [RelayCommand(CanExecute = nameof(CanUseIoU))]
    public void HandleIoUModeClick()
    {
        if (!CanUseIoU)
            return;

        ClearTransactionModes();
        IsIoU = true;
    }

    [RelayCommand]
    public void HandleExcludeModeClick()
    {
        ClearTransactionModes();
        IsExcludedFromBudget = true;
    }

    [RelayCommand]
    public void HandleExcludedIoUModeClick()
    {
        ClearTransactionModes();
        IsIoU = true;
        IsExcludedFromBudget = true;
    }

    private void ClearTransactionModes()
    {
        IsRecurring = false;
        IsInstallments = false;
        IsIoU = false;
        IsExcludedFromBudget = false;
    }

    public void InitializeFromDraft(AddNewTransactionDraft draft)
    {
        ReloadChoicesFromMainViewModel();

        IsExpense = draft.IsExpense;
        IsGoal = draft.IsGoal;
        AmountText = draft.AmountText;
        NameText = draft.Name;
        NoteText = draft.Note;
        IsIoU = draft.IsIoU;
        IsExcludedFromBudget = draft.IsExcludedFromBudget;
        SelectedDate = draft.Date.Date;
        SelectedExpenseCategory = draft.Category ?? ExpenseCategory.Needs;
        SelectedAccount = draft.AccountId is null
            ? Accounts.FirstOrDefault()
            : Accounts.FirstOrDefault(source => source.Id == draft.AccountId.Value) ??
              Accounts.FirstOrDefault();
        SelectedTag = draft.TagId is null
            ? _orderedTags.FirstOrDefault()
            : _orderedTags.FirstOrDefault(tag => tag.Id == draft.TagId.Value) ?? _orderedTags.FirstOrDefault();
        SelectedGoal = draft.GoalId is null
            ? Goals.FirstOrDefault()
            : Goals.FirstOrDefault(goal => goal.Id == draft.GoalId.Value) ?? Goals.FirstOrDefault();
        IsMoreTagsOpen = false;
        if (IsGoal)
            SyncGoalUpdateName();
        _isTransactionTypeLocked = draft.LockTransactionType;
        OnPropertyChanged(nameof(CanChangeTransactionType));
    }

    partial void OnIsExpenseChanged(bool value)
    {
        if (value && IsGoal)
            IsGoal = false;

        OnPropertyChanged(nameof(IsIncome));
        OnPropertyChanged(nameof(CanUseHistory));
        OnPropertyChanged(nameof(CanEditTransactionName));
        OnPropertyChanged(nameof(CanEditCategory));
        OnPropertyChanged(nameof(CanEditTags));
        OnPropertyChanged(nameof(ShowNoteField));
        OnPropertyChanged(nameof(ShowGoalField));
        OnPropertyChanged(nameof(CanUseInstallments));
        OnPropertyChanged(nameof(CanUseIoU));
        OnPropertyChanged(nameof(InstallmentSummaryText));
        OnPropertyChanged(nameof(IoUTooltip));

        if (!CanUseIoU)
            IsIoU = false;

        if (!value || IsGoal)
            IsMoreTagsOpen = false;

        RefreshAccounts();
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
        OnPropertyChanged(nameof(CanEditTransactionName));
        OnPropertyChanged(nameof(CanEditCategory));
        OnPropertyChanged(nameof(CanEditTags));
        OnPropertyChanged(nameof(ShowNoteField));
        OnPropertyChanged(nameof(ShowGoalField));
        OnPropertyChanged(nameof(CanUseInstallments));
        OnPropertyChanged(nameof(CanUseIoU));
        OnPropertyChanged(nameof(InstallmentSummaryText));
        OnPropertyChanged(nameof(IoUTooltip));

        if (value)
        {
            IsInstallments = false;
            IsIoU = false;
            IsMoreTagsOpen = false;
            SyncGoalUpdateName();
        }

        RefreshAccounts();
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
        SelectedAccount = Accounts.FirstOrDefault(source => source.Id == suggestion.AccountId) ??
                                 SelectedAccount;

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

    partial void OnSelectedTagChanged(TagVM? value)
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

                if (!TryResolveRecurringSaveAmount(input, out var recurringAmount, out var recurringAmountValidationMessage))
                    return AddNewTransactionSubmissionResult.Failure(recurringAmountValidationMessage);

                var recurringType = input.IsGoal
                    ? RecurringTransactionType.GoalUpdate
                    : input.IsExpense
                        ? RecurringTransactionType.Expense
                        : RecurringTransactionType.Income;

                var recurringName = input.IsGoal && input.GoalId is not null
                    ? BuildGoalUpdateName((await _appData.GetSavingGoalByIdAsync(input.GoalId.Value))?.Name ?? string.Empty)
                    : input.IsInstallments
                        ? BuildInstallmentRecurringName(input.Name)
                        : BuildExpenseName(input.Name, input.Note, input.IsExpense ? "Recurring Expense" : "Recurring Income");

                var draftSaveResult = await _saveRecurringDraftAsync(new RecurringDraftSaveInput(
                    input.EditingRecurringTransactionId,
                    recurringType,
                    recurringName,
                    recurringAmount,
                    input.RecurringPeriod,
                    recurringTime,
                    input.AccountId,
                    input.IsExpense ? input.Category : null,
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

            var account = await _appData.GetAccountByIdAsync(input.AccountId);
            if (account is null)
                return AddNewTransactionSubmissionResult.Failure("Please select a valid account.");

            if (!TryResolveRecurringSaveAmount(input, out var effectiveSaveAmount, out var recurringAmountMessage))
                return AddNewTransactionSubmissionResult.Failure(recurringAmountMessage);

            if (!TryValidateSpendingAmountAgainstSource(input.IsExpense, input.IsGoal, effectiveSaveAmount, account, out var spendingValidationMessage))
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
                    : input.IsInstallments
                        ? BuildInstallmentRecurringName(input.Name)
                        : BuildExpenseName(input.Name, input.Note, input.IsExpense ? "Recurring Expense" : "Recurring Income");
                recurring.Amount = effectiveSaveAmount;
                recurring.RecurringPeriod = input.RecurringPeriod;
                recurring.RecurringTime = recurringTime;
                recurring.Type = recurringType;
                recurring.Category = input.IsExpense ? input.Category : null;
                recurring.SourceId = input.AccountId;
                recurring.TagId = input.IsGoal ? null : input.TagId;
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

                if (!GoalUpdateTransactionSupport.IsEligibleGoalSourceType(account.AccountType))
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
                var transaction = new Transaction
                {
                    Type = TransactionType.Expense,
                    Name = BuildGoalUpdateName(goal.Name),
                    Amount = input.Amount,
                    OccurredOn = input.Date,
                    Notes = $"Goal update for {goal.Name}",
                    ExpenseCategory = ExpenseCategory.Savings,
                    AccountId = account.Id,
                    TagId = goalUpdateTag.Id,
                    IsPinned = false,
                    IsExcludedFromBudget = input.IsExcludedFromBudget
                };

                goal.CurrentAmount += input.Amount;

                await _appData.AddTransactionAsync(transaction);
                _appData.UpdateSavingGoal(goal);

                ApplyExpenseToAccount(account, input.Amount);
                _appData.UpdateAccount(account);

                await _appData.SaveChangesAsync();
                WeakReferenceMessenger.Default.Send(
                    new RecordLogMemoryMessage(new AddTransactionMemoryAction(
                        TransactionMemorySnapshot.Create(transaction))));

                invalidationScope |= DashboardDataInvalidationScope.SavingGoals;
            }
            else if (input.IsExpense)
            {
                var tag = await _appData.GetTagByIdAsync(input.TagId!.Value);
                if (tag is null)
                    return AddNewTransactionSubmissionResult.Failure("Please select a valid tag.");

                var budgetPolicyResult = await ApplyExpenseBudgetPolicyAsync(input.Category!.Value, input.Amount, input.Date);
                if (!budgetPolicyResult.IsSuccess)
                    return budgetPolicyResult;

                var transaction = new Transaction
                {
                    Type = TransactionType.Expense,
                    Name = BuildExpenseName(input.Name, input.Note, tag.Name),
                    Amount = input.Amount,
                    OccurredOn = input.Date,
                    Notes = input.Note,
                    ExpenseCategory = input.Category!.Value,
                    AccountId = account.Id,
                    TagId = tag.Id,
                    IsPinned = input.IsPinned,
                    IsIoU = input.IsIoU,
                    IsExcludedFromBudget = input.IsExcludedFromBudget
                };

                await _appData.AddTransactionAsync(transaction);

                ApplyExpenseToAccount(account, input.Amount);
                _appData.UpdateAccount(account);

                await _appData.SaveChangesAsync();
                WeakReferenceMessenger.Default.Send(
                    new RecordLogMemoryMessage(new AddTransactionMemoryAction(
                        TransactionMemorySnapshot.Create(transaction))));
            }
            else
            {
                var transaction = new Transaction
                {
                    Type = TransactionType.Income,
                    Name = input.Name,
                    Amount = input.Amount,
                    OccurredOn = input.Date,
                    Notes = input.Note,
                    AccountId = account.Id,
                    TagId = input.TagId,
                    IsPinned = input.IsPinned,
                    IsIoU = input.IsIoU,
                    IsExcludedFromBudget = input.IsExcludedFromBudget
                };

                await _appData.AddTransactionAsync(transaction);

                ApplyIncomeToAccount(account, input.Amount);
                _appData.UpdateAccount(account);

                await _appData.SaveChangesAsync();
                WeakReferenceMessenger.Default.Send(
                    new RecordLogMemoryMessage(new AddTransactionMemoryAction(
                        TransactionMemorySnapshot.Create(transaction))));
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
                var incomeLogs = (await _appData.GetTransactionsAsync(cancellationToken))
                    .Where(transaction => transaction.Type == TransactionType.Income);
                return incomeLogs.Any(log => IsSimilarIncomeTransaction(log, input, candidateName));
            }

            var expenseLogs = (await _appData.GetTransactionsAsync(cancellationToken))
                .Where(transaction => transaction.Type == TransactionType.Expense);
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
        InstallmentEndDate = DateTime.Today;
        IsInstallments = false;
        IsRecurring = false;
        IsPinned = false;
        IsIoU = false;
        IsExcludedFromBudget = false;
        IsRecurringModeLocked = false;
        _isTransactionTypeLocked = false;
        SetPopupPurpose(TransactionPopupPurpose.AddNewTransaction);
        OnPropertyChanged(nameof(CanChangeTransactionType));
        SelectedRecurringPeriod = RecurringPeriod.Monthly;
        RecurringTimeText = GetDefaultRecurringTimeText(SelectedRecurringPeriod);
        SelectedExpenseCategory = ExpenseCategory.Needs;
        SelectedAccount = Accounts.FirstOrDefault(account => account.IsDefault) ?? Accounts.FirstOrDefault();
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
        else if (!IsGoal)
        {
            if (SelectedTag is null)
            {
                validationMessage = "Please choose a tag.";
                return false;
            }

            category = IsExpense ? SelectedExpenseCategory : null;
            tagId = SelectedTag.Id;
        }

        if (SelectedAccount is null)
        {
            validationMessage = "Please choose a account.";
            return false;
        }

        if (IsInstallments && !TryResolveInstallmentCount(
                SelectedRecurringPeriod,
                RecurringTimeText,
                InstallmentEndDate,
                InstallmentCalculationDate,
                out _,
                out validationMessage))
        {
            return false;
        }

        input = new QuickTransactionInput(
            IsExpense,
            IsGoal,
            IsRecurringTransactionMode,
            IsInstallments,
            IsPinned,
            IsIoU,
            IsExcludedFromBudget,
            _editingRecurringTransactionId,
            SelectedRecurringPeriod,
            NameText.Trim(),
            AmountText,
            SelectedAccount.Id,
            SelectedDate.Date.Add(DateTime.Now.TimeOfDay),
            InstallmentEndDate.Date,
            RecurringTimeText.Trim(),
            NoteText.Trim(),
            category,
            tagId,
            goalId);

        return true;
    }

    private void ReloadChoicesFromMainViewModel()
    {
        _availableAccounts.Clear();
        var sourceCatalog = _accountsOverride ?? _mainViewModel.BudgetPanel.Accounts;
        _availableAccounts.AddRange(sourceCatalog.Where(source => source.IsEnabled));

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
        RefreshAccounts();
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
        var expenseLogs = (await _appData.GetTransactionsAsync())
            .Where(transaction => transaction.Type == TransactionType.Expense);
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
        IEnumerable<Transaction> expenseLogs,
        BudgetAllocationPeriod period)
    {
        return expenseLogs
            .Where(log => !log.IsForDeletion)
            .Where(log => !log.IsExcludedFromBudget)
            .Where(log => log.OccurredOn.Date >= period.Start && log.OccurredOn.Date <= period.End)
            .Where(log => log.ExpenseCategory.HasValue)
            .GroupBy(log => log.ExpenseCategory!.Value)
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

    private void PromoteTagToVisibleStart(TagVM selectedTag)
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

    private static string BuildGoalUpdateDisplayName(string goalName)
    {
        var trimmedGoalName = goalName.Trim();
        return string.IsNullOrWhiteSpace(trimmedGoalName)
            ? GoalUpdateTransactionSupport.GoalUpdateTagName
            : $"{GoalUpdateTransactionSupport.GoalUpdateTagName} for {trimmedGoalName}";
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
        var tags = await _appData.GetTagsAsync(cancellationToken);
        return tags
            .Where(tag => string.Equals(
                tag.Name?.Trim(),
                GoalUpdateTransactionSupport.GoalUpdateTagName,
                StringComparison.OrdinalIgnoreCase))
            .Select(tag => tag.Id)
            .ToHashSet();
    }

    private static bool IsSimilarExpenseTransaction(
        Transaction log,
        QuickTransactionInput input,
        string candidateName,
        bool candidateIsGoalUpdate,
        IReadOnlySet<int> goalUpdateTagIds)
    {
        if (log.IsForDeletion ||
            log.AccountId != input.AccountId ||
            log.Type != TransactionType.Expense ||
            !IsSameTransactionName(log.Name, candidateName) ||
            !IsSimilarAmount(log.Amount, input.Amount))
        {
            return false;
        }

        return IsGoalUpdateExpenseLog(log, goalUpdateTagIds) == candidateIsGoalUpdate;
    }

    private static bool IsSimilarIncomeTransaction(
        Transaction log,
        QuickTransactionInput input,
        string candidateName)
    {
        return log.Type == TransactionType.Income && log.AccountId == input.AccountId &&
               IsSameTransactionName(log.Name, candidateName) &&
               IsSimilarAmount(log.Amount, input.Amount);
    }

    private static bool IsGoalUpdateExpenseLog(Transaction log, IReadOnlySet<int> goalUpdateTagIds)
    {
        var tagName = log.Tag?.Name;
        if (!string.IsNullOrWhiteSpace(tagName))
            return string.Equals(
                tagName.Trim(),
                GoalUpdateTransactionSupport.GoalUpdateTagName,
                StringComparison.OrdinalIgnoreCase);

        if (log.TagId is > 0 && goalUpdateTagIds.Count > 0)
            return goalUpdateTagIds.Contains(log.TagId.Value);

        var expenseName = log.Name.Trim();
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
            var transactions = await _appData.GetTransactionsAsync();

            if (requestVersion != _transactionNameSuggestionRequestVersion)
                return;

            var suggestions = BuildTransactionNameSuggestions(
                transactions.Where(transaction => transaction.Type == TransactionType.Expense),
                transactions.Where(transaction => transaction.Type == TransactionType.Income),
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
        IEnumerable<Transaction> expenseLogs,
        IEnumerable<Transaction> incomeLogs,
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
        IEnumerable<Transaction> expenseLogs,
        string query)
    {
        return expenseLogs
            .Where(log => !log.IsForDeletion)
            .Where(log => log.Type == TransactionType.Expense && !string.IsNullOrWhiteSpace(log.Name))
            .Where(log => log.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(log => log.OccurredOn)
            .ThenByDescending(log => log.LoggedOn)
            .Select(log => new AddNewTransactionSuggestion(
                log.Name,
                log.Amount,
                log.AccountId,
                log.Account?.Name ?? string.Empty,
                log.Notes,
                log.ExpenseCategory,
                log.TagId,
                null));
    }

    private static IEnumerable<AddNewTransactionSuggestion> BuildIncomeTransactionNameSuggestions(
        IEnumerable<Transaction> incomeLogs,
        string query)
    {
        return incomeLogs
            .Where(log => log.Type == TransactionType.Income && !string.IsNullOrWhiteSpace(log.Name))
            .Where(log => log.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(log => log.OccurredOn)
            .ThenByDescending(log => log.LoggedOn)
            .Select(log => new AddNewTransactionSuggestion(
                log.Name,
                log.Amount,
                log.AccountId,
                log.Account?.Name ?? string.Empty,
                log.Notes,
                null,
                null,
                null));
    }

    private static bool TryValidateSpendingAmountAgainstSource(
        bool isExpense,
        bool isGoal,
        decimal amount,
        AccountVM source,
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

        if (!TryValidateMaximumSpending(source.MaximumSpending, source.AccountType, source.SpentAmount, source.MoneyOut, amount, out validationMessage))
            return false;

        return TryValidateSpendingCapacity(source.AccountType, source.Balance, source.AccountLimit, source.SpentAmount, amount, out validationMessage);
    }

    private static bool TryValidateSpendingAmountAgainstSource(
        bool isExpense,
        bool isGoal,
        decimal amount,
        Account source,
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
        if (!TryValidateMaximumSpending(source.MaximumSpending, source.AccountType, source.SpentAmount, persistedMoneyOut, amount, out validationMessage))
            return false;

        return TryValidateSpendingCapacity(source.AccountType, source.Balance, source.AccountLimit, source.SpentAmount, amount, out validationMessage);
    }

    private static decimal GetPersistedMoneyOut(Account source)
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
        AccountType sourceType,
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

        var projectedSpending = sourceType == AccountType.Credit
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
        AccountType sourceType,
        decimal balance,
        decimal accountLimit,
        decimal spentAmount,
        decimal amount,
        out string validationMessage)
    {
        if (sourceType == AccountType.Credit)
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

    private static void ApplyExpenseToAccount(Account account, decimal amount)
    {
        if (account.AccountType == AccountType.Credit)
        {
            account.SpentAmount += amount;
            return;
        }

        account.Balance -= amount;
    }

    private static void ApplyIncomeToAccount(Account account, decimal amount)
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

    private string GetFirstValidationMessage()
    {
        foreach (var propertyName in new[]
                 {
                     nameof(NameText),
                     nameof(AmountText),
                     nameof(SelectedAccount),
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
            IsInstallments,
            IsPinned,
            IsIoU,
            IsExcludedFromBudget,
            SelectedRecurringPeriod,
            NameText ?? string.Empty,
            AmountText,
            RecurringTimeText ?? string.Empty,
            NoteText ?? string.Empty,
            SelectedDate.Date,
            InstallmentEndDate.Date,
            SelectedExpenseCategory,
            SelectedAccount?.Id ?? NoAccountId,
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
            if (IsGoal)
            {
                var expenseLogs = await _appData.GetTransactionsAsync(cancellationToken);
                PinnedHistory.Reset([]);
                TransactionHistory.Reset(SelectedGoal is null
                    ? []
                    : AddNewTransactionHistoryBuilder.BuildGoalUpdateHistory(expenseLogs, SelectedGoal.Name));
                return;
            }

            if (IsExpense)
            {
                var expenseLogs = await _appData.GetTransactionsAsync(cancellationToken);
                PinnedHistory.Reset(AddNewTransactionHistoryBuilder.BuildPinnedExpenses(expenseLogs));
                TransactionHistory.Reset(AddNewTransactionHistoryBuilder.BuildExpenseHistory(expenseLogs));
                return;
            }

            var incomeLogs = await _appData.GetTransactionsAsync(cancellationToken);
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
        if (item.IsGoalUpdate)
            IsGoal = true;
        else
        {
            IsExpense = item.IsExpense;
            IsGoal = false;
        }

        NameText = item.Name;
        AmountText = item.Amount;
        NoteText = item.Note;
        SelectedDate = item.Date.Date;
        IsPinned = item.IsPinned;
        SelectedAccount = Accounts.FirstOrDefault(source => source.Id == item.AccountId) ??
                                 SelectedAccount;

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
               && IsValidationSuccess(ValidateSelectedAccount(SelectedAccount, CreateValidationContext()))
               && IsValidationSuccess(ValidateSelectedTag(SelectedTag, CreateValidationContext()))
               && IsValidationSuccess(ValidateSelectedGoal(SelectedGoal, CreateValidationContext()))
               && IsValidationSuccess(ValidateRecurringTimeText(RecurringTimeText, CreateValidationContext()))
               && IsInstallmentInputValid();
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
            return "Overflowing Balance";

        if (message.Contains("account limit", StringComparison.OrdinalIgnoreCase))
            return "Account Limit";

        if (message.Contains("spending limit", StringComparison.OrdinalIgnoreCase))
            return "Tag Limit";

        return "Invalid Amount";
    }

    private bool IsInstallmentInputValid()
    {
        return !IsInstallments
               || TryResolveInstallmentCount(
                   SelectedRecurringPeriod,
                   RecurringTimeText,
                   InstallmentEndDate,
                   InstallmentCalculationDate,
                   out _,
                   out _);
    }

    private void RefreshAccounts()
    {
        var selectedAccountId = SelectedAccount?.Id;

        var filteredSources = _availableAccounts
            .Where(source =>
                IsGoal
                    ? GoalUpdateTransactionSupport.IsEligibleGoalSourceType(source.AccountType)
                    : IsExpense || source.AccountType != AccountType.Credit)
            .OrderBy(GetAccountTypeSortOrder)
            .ThenByDescending(GetAccountWithinTypeSortValue)
            .ThenBy(source => source.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        ReplaceCollection(Accounts, filteredSources);

        SelectedAccount = selectedAccountId is null
            ? Accounts.FirstOrDefault(account => account.IsDefault) ?? Accounts.FirstOrDefault()
            : Accounts.FirstOrDefault(source => source.Id == selectedAccountId.Value) ??
              Accounts.FirstOrDefault(account => account.IsDefault) ?? Accounts.FirstOrDefault();
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

        if (viewModel.SelectedAccount is null)
            return ValidationResult.Success;

        var amountToValidate = value;
        if (viewModel.IsInstallments)
        {
            if (!TryResolveInstallmentCount(
                viewModel.SelectedRecurringPeriod,
                viewModel.RecurringTimeText,
                viewModel.InstallmentEndDate,
                viewModel.InstallmentCalculationDate,
                out var installmentCount,
                out _))
            {
                return ValidationResult.Success;
            }

            amountToValidate = CalculateInstallmentAmount(value, installmentCount);
        }

        if (!TryValidateSpendingAmountAgainstSource(viewModel.IsExpense, viewModel.IsGoal, amountToValidate, viewModel.SelectedAccount, out var validationMessage))
            return new ValidationResult(validationMessage);

        if (!viewModel.TryValidateSpendingAmountAgainstTagLimit(amountToValidate, out var tagLimitValidationMessage))
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
            var currentTagSpending = _appData.GetTransactionsAsync().GetAwaiter().GetResult()
                .Where(log => log.Type == TransactionType.Expense && !log.IsForDeletion && !log.IsExcludedFromBudget)
                .Where(log => log.OccurredOn.Date >= currentPeriod.Start && log.OccurredOn.Date <= currentPeriod.End)
                .Where(log => log.TagId == tag.Id || log.Tag?.Id == tag.Id)
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

    public static ValidationResult? ValidateSelectedAccount(AccountVM? value, ValidationContext validationContext)
    {
        _ = validationContext;
        return value is null
            ? new ValidationResult("Please choose a account.")
            : ValidationResult.Success;
    }

    public static ValidationResult? ValidateSelectedTag(TagVM? value, ValidationContext validationContext)
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
        if (!viewModel.IsRecurringTransactionMode || viewModel.SelectedRecurringPeriod == RecurringPeriod.None)
            return ValidationResult.Success;

        return TryNormalizeRecurringTime(viewModel.SelectedRecurringPeriod, value?.Trim() ?? string.Empty, out _)
            ? ValidationResult.Success
            : new ValidationResult(GetRecurringTimeValidationMessage(viewModel.SelectedRecurringPeriod));
    }

    internal static IEnumerable<TagVM> ProjectNonSystemTags(IEnumerable<Tag> tags)
    {
        return tags
            .Where(tag => !tag.IsSystemTag)
            .OrderBy(tag => tag.Name, StringComparer.OrdinalIgnoreCase)
            .Select(tag => new TagVM
            {
                Id = tag.Id,
                Name = tag.Name,
                HexCode = tag.HexCode,
                IsSystemTag = false,
                SpendingLimit = tag.SpendingLimit
            });
    }

    internal static IEnumerable<TagVM> OrderNonSystemTags(IEnumerable<TagVM> tags)
    {
        return tags
            .Where(tag => !tag.IsSystemTag)
            .OrderBy(tag => tag.Name, StringComparer.OrdinalIgnoreCase);
    }

    internal static int GetAccountTypeSortOrder(AccountVM source)
    {
        return source.AccountType switch
        {
            AccountType.Checking => 0,
            AccountType.Cash => 1,
            AccountType.Credit => 2,
            AccountType.Saving => 3,
            _ => 5
        };
    }

    internal static decimal GetAccountWithinTypeSortValue(AccountVM source)
    {
        return source.AccountType switch
        {
            AccountType.Checking or AccountType.Cash => source.Balance,
            AccountType.Credit => source.AccountLimit - source.SpentAmount,
            AccountType.Saving => source.Balance,
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
        int AccountId,
        string AccountName,
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
        int? AccountId,
        DateTime Date,
        string Note,
        ExpenseCategory? Category,
        int? TagId,
        bool IsGoal = false,
        int? GoalId = null,
        bool IsIoU = false,
        bool IsExcludedFromBudget = false,
        bool LockTransactionType = false);

    public readonly record struct RecurringDraftSaveInput(
        int? EditingRecurringTransactionId,
        RecurringTransactionType Type,
        string Name,
        decimal Amount,
        RecurringPeriod RecurringPeriod,
        int RecurringTime,
        int AccountId,
        ExpenseCategory? Category,
        int? TagId,
        int? GoalId);

    public readonly record struct RecurringDraftSnapshot(
        int? EditingRecurringTransactionId,
        RecurringTransactionType Type,
        string Name,
        decimal Amount,
        RecurringPeriod RecurringPeriod,
        int RecurringTime,
        int AccountId,
        ExpenseCategory? Category,
        int? TagId,
        int? GoalId);

    private readonly record struct QuickTransactionInput(
        bool IsExpense,
        bool IsGoal,
        bool IsRecurring,
        bool IsInstallments,
        bool IsPinned,
        bool IsIoU,
        bool IsExcludedFromBudget,
        int? EditingRecurringTransactionId,
        RecurringPeriod RecurringPeriod,
        string Name,
        decimal Amount,
        int AccountId,
        DateTime Date,
        DateTime InstallmentEndDate,
        string RecurringTimeText,
        string Note,
        ExpenseCategory? Category,
        int? TagId,
        int? GoalId);

    private readonly record struct FormState(
        bool IsExpense,
        bool IsGoal,
        bool IsRecurring,
        bool IsInstallments,
        bool IsPinned,
        bool IsIoU,
        bool IsExcludedFromBudget,
        RecurringPeriod SelectedRecurringPeriod,
        string NameText,
        decimal AmountText,
        string RecurringTimeText,
        string NoteText,
        DateTime SelectedDate,
        DateTime InstallmentEndDate,
        ExpenseCategory SelectedExpenseCategory,
        int SelectedAccountId,
        int SelectedTagId,
        int SelectedGoalId);

    private readonly record struct PendingTransactionInputState(
        string NameText,
        decimal AmountText);

    private int? _editingRecurringTransactionId;

    public void InitializeRecurringMode(bool isLocked)
    {
        SetPopupPurpose(TransactionPopupPurpose.AddRecurringTransaction);
        _isTransactionTypeLocked = isLocked;
        OnPropertyChanged(nameof(CanChangeTransactionType));
        IsRecurringModeLocked = isLocked;
        IsInstallments = false;
        IsRecurring = true;
    }

    public async Task<bool> InitializeFromRecurringTransactionAsync(int recurringTransactionId, CancellationToken cancellationToken = default)
    {
        var recurring = await _appData.GetRecurringTransactionByIdAsync(recurringTransactionId, cancellationToken);
        if (recurring is null)
            return false;

        _editingRecurringTransactionId = recurring.Id;
        SetPopupPurpose(TransactionPopupPurpose.EditRecurringTransaction);
        _isTransactionTypeLocked = true;
        OnPropertyChanged(nameof(CanChangeTransactionType));
        IsRecurringModeLocked = true;
        IsInstallments = false;
        IsRecurring = true;
        IsExpense = recurring.Type == RecurringTransactionType.Expense;
        IsGoal = recurring.Type == RecurringTransactionType.GoalUpdate;
        NameText = recurring.Name;
        AmountText = recurring.Amount;
        SelectedExpenseCategory = recurring.Category ?? ExpenseCategory.Needs;
        SelectedRecurringPeriod = recurring.RecurringPeriod;
        RecurringTimeText = recurring.RecurringPeriod == RecurringPeriod.None
            ? string.Empty
            : recurring.RecurringTime.ToString(CultureInfo.InvariantCulture);
        SelectedAccount = Accounts.FirstOrDefault(source => source.Id == recurring.SourceId) ?? Accounts.FirstOrDefault();
        SelectedTag = recurring.TagId is > 0 ? _orderedTags.FirstOrDefault(tag => tag.Id == recurring.TagId.Value) : _orderedTags.FirstOrDefault();
        SelectedGoal = recurring.GoalId is > 0 ? Goals.FirstOrDefault(goal => goal.Id == recurring.GoalId.Value) : Goals.FirstOrDefault();
        return true;
    }

    public void InitializeFromRecurringDraft(RecurringDraftSnapshot draft)
    {
        _editingRecurringTransactionId = draft.EditingRecurringTransactionId;
        SetPopupPurpose(draft.EditingRecurringTransactionId is > 0
            ? TransactionPopupPurpose.EditRecurringTransaction
            : TransactionPopupPurpose.AddRecurringTransaction);
        _isTransactionTypeLocked = true;
        OnPropertyChanged(nameof(CanChangeTransactionType));
        IsRecurringModeLocked = true;
        IsInstallments = false;
        IsRecurring = true;
        IsExpense = draft.Type == RecurringTransactionType.Expense;
        IsGoal = draft.Type == RecurringTransactionType.GoalUpdate;
        NameText = draft.Name;
        AmountText = draft.Amount;
        SelectedExpenseCategory = draft.Category ?? ExpenseCategory.Needs;
        SelectedRecurringPeriod = draft.RecurringPeriod;
        RecurringTimeText = draft.RecurringPeriod == RecurringPeriod.None
            ? string.Empty
            : draft.RecurringTime.ToString(CultureInfo.InvariantCulture);
        SelectedAccount = Accounts.FirstOrDefault(source => source.Id == draft.AccountId) ?? Accounts.FirstOrDefault();
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

    private string BuildInstallmentSummaryText()
    {
        if (!IsInstallments)
            return string.Empty;

        if (!TryResolveInstallmentCount(
                SelectedRecurringPeriod,
                RecurringTimeText,
                InstallmentEndDate,
                InstallmentCalculationDate,
                out var count,
                out _))
            return string.Empty;

        var installmentAmount = CalculateInstallmentAmount(AmountText, count);
        var amountText = MoneyFormatUtility.ToCompactText(installmentAmount, CultureInfo.CurrentCulture);
        var recurrenceLabel = FormatRecurringScheduleLabel(SelectedRecurringPeriod, RecurringTimeText);
        var verb = IsExpense ? "paid" : "earned";
        return $"The installment will be {amountText}, {verb} every {recurrenceLabel}";
    }

    private bool TryResolveRecurringSaveAmount(
        QuickTransactionInput input,
        out decimal amount,
        out string validationMessage)
    {
        amount = input.Amount;
        validationMessage = string.Empty;

        if (!input.IsInstallments)
            return true;

        if (!TryResolveInstallmentCount(
                input.RecurringPeriod,
                input.RecurringTimeText,
                input.InstallmentEndDate,
                InstallmentCalculationDate,
                out var count,
                out validationMessage))
            return false;

        amount = CalculateInstallmentAmount(input.Amount, count);
        return true;
    }

    private static decimal CalculateInstallmentAmount(decimal totalAmount, int count)
    {
        return decimal.Round(totalAmount / count, 2, MidpointRounding.AwayFromZero);
    }

    private static string BuildInstallmentRecurringName(string name)
    {
        return $"Installments for {name.Trim()}";
    }

    private static bool TryResolveInstallmentCount(
        RecurringPeriod period,
        string recurringTimeText,
        DateTime endDate,
        DateTime today,
        out int count,
        out string validationMessage)
    {
        count = 0;
        validationMessage = string.Empty;

        if (period == RecurringPeriod.None)
        {
            validationMessage = "Installments need a weekly, biweekly, or monthly recurrence.";
            return false;
        }

        if (!TryNormalizeRecurringTime(period, recurringTimeText?.Trim() ?? string.Empty, out var recurringTime))
        {
            validationMessage = GetRecurringTimeValidationMessage(period);
            return false;
        }

        today = today.Date;
        endDate = endDate.Date;
        if (endDate < today)
        {
            validationMessage = "Installment end date must be today or later.";
            return false;
        }

        var occurrence = FindClosestOccurrence(today, period, recurringTime);
        while (occurrence <= endDate)
        {
            count++;
            occurrence = AddOccurrence(occurrence, period);
        }

        if (count > 0)
            return true;

        validationMessage = "Installment end date must include at least one recurrence.";
        return false;
    }

    private static DateTime FindClosestOccurrence(DateTime today, RecurringPeriod period, int recurringTime)
    {
        if (period == RecurringPeriod.Monthly)
        {
            var candidate = new DateTime(today.Year, today.Month, recurringTime);
            var previous = candidate <= today ? candidate : candidate.AddMonths(-1);
            var next = candidate >= today ? candidate : candidate.AddMonths(1);
            return IsCloserToToday(next, previous, today) ? next : previous;
        }

        var previousDate = today;
        while (GetIsoDayOfWeek(previousDate.DayOfWeek) != recurringTime)
            previousDate = previousDate.AddDays(-1);

        var nextDate = today;
        while (GetIsoDayOfWeek(nextDate.DayOfWeek) != recurringTime)
            nextDate = nextDate.AddDays(1);

        return IsCloserToToday(nextDate, previousDate, today) ? nextDate : previousDate;
    }

    private static bool IsCloserToToday(DateTime candidate, DateTime comparison, DateTime today)
    {
        return Math.Abs((candidate.Date - today.Date).TotalDays)
               < Math.Abs((comparison.Date - today.Date).TotalDays);
    }

    private static DateTime AddOccurrence(DateTime occurrence, RecurringPeriod period)
    {
        return period switch
        {
            RecurringPeriod.Weekly => occurrence.AddDays(7),
            RecurringPeriod.Biweekly => occurrence.AddDays(14),
            RecurringPeriod.Monthly => occurrence.AddMonths(1),
            _ => occurrence
        };
    }

    private static string FormatRecurringScheduleLabel(RecurringPeriod period, string recurringTimeText)
    {
        if (!TryNormalizeRecurringTime(period, recurringTimeText?.Trim() ?? string.Empty, out var recurringTime))
            return string.Empty;

        if (period == RecurringPeriod.Monthly)
            return FormatOrdinal(recurringTime);

        var option = recurringTime is >= 1 and <= 7
            ? CultureInfo.CurrentCulture.DateTimeFormat.DayNames[recurringTime % 7]
            : string.Empty;
        return option;
    }

    private static string FormatOrdinal(int value)
    {
        var suffix = (value % 100) is 11 or 12 or 13
            ? "th"
            : (value % 10) switch
            {
                1 => "st",
                2 => "nd",
                3 => "rd",
                _ => "th"
            };

        return value.ToString(CultureInfo.InvariantCulture) + suffix;
    }

    private static int GetIsoDayOfWeek(DayOfWeek dayOfWeek)
    {
        return dayOfWeek == DayOfWeek.Sunday ? 7 : (int)dayOfWeek;
    }

    private static string GetRecurringTimeValidationMessage(RecurringPeriod period)
    {
        return IsWeekdayRecurringPeriod(period)
            ? "Recurring weekday must be between Monday and Sunday."
            : "Recurring day must be between 1 and 28.";
    }

    public sealed record RecurringTimeOption(string Label, string Value);

    private enum TransactionPopupPurpose
    {
        AddNewTransaction,
        AddRecurringTransaction,
        EditRecurringTransaction
    }

    private void SetPopupPurpose(TransactionPopupPurpose purpose)
    {
        if (_popupPurpose == purpose)
            return;

        _popupPurpose = purpose;
        if (!CanPinTransaction)
            IsPinned = false;

        OnPropertyChanged(nameof(PopupTitle));
        OnPropertyChanged(nameof(CanPinTransaction));
    }

    private void SyncGoalUpdateName()
    {
        if (SelectedGoal is not { } goal || string.IsNullOrWhiteSpace(goal.Name))
            return;

        NameText = BuildGoalUpdateDisplayName(goal.Name);
    }
}
