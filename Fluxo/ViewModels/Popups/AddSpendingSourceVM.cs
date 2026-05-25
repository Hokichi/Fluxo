using System.Globalization;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Resources.Resources.Messages;
using Fluxo.Services.History;
using Fluxo.Services.Logging;
using Fluxo.ViewModels.Shell;
using MainVM = Fluxo.ViewModels.Shell.Main.MainVM;
using Fluxo.ViewModels.Popups.Helpers;

namespace Fluxo.ViewModels.Popups;

public partial class AddSpendingSourceVM : ObservableValidator
{
    private const string BalanceUpdateTagColor = "#e8ca5f";
    private const string BalanceUpdateTagName = "Balance Update";
    private const int MaxNameLength = 256;
    private const decimal PercentageMaximum = 100m;

    private readonly MainVM _mainViewModel;
    private readonly IAppDataService _appData;
    private readonly HashSet<string> _knownSpendingSourceNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<AddSpendingSourceInput, Task<AddSpendingSourceResult>>? _saveDraftAsync;
    private readonly Func<int?, Task<IReadOnlyList<DeductSourceOption>>>? _loadDraftDeductSourcesAsync;
    private FormState _initialState;
    private bool _isChangeTrackingInitialized;
    private bool _isApyValidationActive;
    private bool _isMaximumSpendingModified;
    private bool _isMaximumSpendingValidationActive;
    private bool _isMinimumPaymentValidationActive;
    private bool _isNameValidationActive;
    private bool _isSpentAmountValidationActive;

    [ObservableProperty] private decimal _accountLimitText;
    [ObservableProperty]
    [CustomValidation(typeof(AddSpendingSourceVM), nameof(ValidateApyText))]
    private decimal _apyText;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isEnabled = true;
    [ObservableProperty]
    [CustomValidation(typeof(AddSpendingSourceVM), nameof(ValidateMaximumSpendingText))]
    private decimal _maximumSpendingText;
    [ObservableProperty]
    [CustomValidation(typeof(AddSpendingSourceVM), nameof(ValidateMinimumPaymentText))]
    private decimal _minimumPaymentText;
    [ObservableProperty] private string _monthlyDueDateText = GetDefaultMonthlyDueDateText();
    [ObservableProperty]
    [CustomValidation(typeof(AddSpendingSourceVM), nameof(ValidateNameText))]
    private string _nameText = string.Empty;
    [ObservableProperty] private decimal _primaryAmountText;
    [ObservableProperty] private int? _selectedDeductSource;
    [ObservableProperty] private SpendingSourceType _selectedSpendingSourceType = SpendingSourceType.Checking;
    [ObservableProperty] private bool _showOnUI = true;
    [ObservableProperty]
    [CustomValidation(typeof(AddSpendingSourceVM), nameof(ValidateSpentAmountText))]
    private decimal _spentAmountText;

    public int? EditingId { get; set; }

    public AddSpendingSourceVM(
        MainVM mainViewModel,
        IAppDataService appData,
        Func<AddSpendingSourceInput, Task<AddSpendingSourceResult>>? saveDraftAsync = null,
        Func<int?, Task<IReadOnlyList<DeductSourceOption>>>? loadDraftDeductSourcesAsync = null)
    {
        _mainViewModel = mainViewModel;
        _appData = appData;
        _saveDraftAsync = saveDraftAsync;
        _loadDraftDeductSourcesAsync = loadDraftDeductSourcesAsync;
        DeductSourcesView = SpendingSourceComboBoxViewFactory.CreateGroupedByTypeThenName(
            DeductSources,
            nameof(DeductSourceOption.TypeDisplayName),
            nameof(DeductSourceOption.SpendingSourceType),
            nameof(DeductSourceOption.Name));
        ErrorsChanged += (_, e) =>
        {
            OnPropertyChanged(nameof(CanSave));

            if (e.PropertyName == nameof(NameText))
                OnPropertyChanged(nameof(NameValidationHint));

            if (e.PropertyName == nameof(MaximumSpendingText))
                OnPropertyChanged(nameof(MaximumSpendingValidationHint));

            if (e.PropertyName == nameof(SpentAmountText) || e.PropertyName == nameof(AccountLimitText))
                OnPropertyChanged(nameof(SpentAmountValidationHint));

            if (e.PropertyName == nameof(MinimumPaymentText))
                OnPropertyChanged(nameof(MinimumPaymentValidationHint));

            if (e.PropertyName == nameof(ApyText))
                OnPropertyChanged(nameof(ApyValidationHint));
        };
        _initialState = CaptureState();
    }

    public IReadOnlyList<SpendingSourceTypeOption> SpendingSourceTypes { get; } =
    [
        new("Checking", SpendingSourceType.Checking),
        new("Cash", SpendingSourceType.Cash),
        new("Credit", SpendingSourceType.Credit),
        new("BNPL", SpendingSourceType.BNPL),
        new("Savings", SpendingSourceType.Saving)
    ];

    public ObservableCollection<DeductSourceOption> DeductSources { get; } = [];
    public ICollectionView DeductSourcesView { get; }

    public bool CanSave => !IsBusy && !HasErrors && AreRequiredFieldsFilled();
    public bool HasChanges => _isChangeTrackingInitialized && !CaptureState().Equals(_initialState);
    public bool IsEditMode => EditingId.HasValue;
    public bool IsSourceTypeSelectionEnabled => !IsEditMode;
    public string PopupTitle => IsEditMode ? "Edit Spending Source" : "Add New Income Source";
    public string HeaderTitle => PopupTitle;

    public string HeaderDescription => IsEditMode
        ? "Update this source for checking, cash, credit, BNPL, or savings."
        : "Set up a new source for checking, cash, credit, BNPL, or savings.";

    public string ValidationDialogTitle => PopupTitle;
    public string NameValidationHint => GetValidationHint(nameof(NameText));
    public string MaximumSpendingValidationHint => GetValidationHint(nameof(MaximumSpendingText));
    public string SpentAmountValidationHint => GetValidationHint(nameof(SpentAmountText));
    public string MinimumPaymentValidationHint => GetValidationHint(nameof(MinimumPaymentText));
    public string ApyValidationHint => GetValidationHint(nameof(ApyText));
    public string MaximumSpendingPlaceholderText => FormatPlaceholderAmount(GetMaximumSpendingPlaceholderValue());

    public void BeginChangeTracking()
    {
        _initialState = CaptureState();
        _isChangeTrackingInitialized = true;
        NotifyFormStateChanged();
    }

    public void InitializeFromSpendingSource(SpendingSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        EditingId = source.Id;
        NameText = source.Name;
        SelectedSpendingSourceType = source.SpendingSourceType;
        ShowOnUI = source.ShowOnUI;
        IsEnabled = source.IsEnabled;

        if (source.SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL)
        {
            PrimaryAmountText = source.SpentAmount;
            SpentAmountText = source.SpentAmount;
            AccountLimitText = source.AccountLimit;
            MaximumSpendingText = source.MaximumSpending;
            MinimumPaymentText = source.MinimumPayment ?? 0m;
            MonthlyDueDateText = MonthlyDueDateHelper.Normalize(source.MonthlyDueDate)?
                .ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            SelectedDeductSource = source.DeductSource;
        }
        else
        {
            PrimaryAmountText = source.Balance;
            MaximumSpendingText = source.MaximumSpending;
        }

        if (source.SpendingSourceType == SpendingSourceType.Saving && source.InterestRate.HasValue)
            ApyText = source.InterestRate.Value;

        OnPropertyChanged(nameof(IsSourceTypeSelectionEnabled));
        ResetValidationState();
    }

    public void ResetAfterSaveAndCreateNew()
    {
        AccountLimitText = 0m;
        ApyText = 0m;
        IsEnabled = true;
        MaximumSpendingText = 0m;
        MinimumPaymentText = 0m;
        MonthlyDueDateText = GetDefaultMonthlyDueDateText();
        NameText = string.Empty;
        PrimaryAmountText = 0m;
        SelectedSpendingSourceType = SpendingSourceType.Checking;
        ShowOnUI = true;
        SpentAmountText = 0m;
        SelectedDeductSource = null;
        ResetValidationState();
        BeginChangeTracking();
    }

    public void ValidateNameField()
    {
        _isNameValidationActive = true;
        ValidateProperty(NameText, nameof(NameText));
    }

    public void ValidateMaximumSpendingField()
    {
        _isMaximumSpendingValidationActive = true;
        ValidateProperty(MaximumSpendingText, nameof(MaximumSpendingText));
    }

    public void ValidateSpentAmountField()
    {
        _isSpentAmountValidationActive = true;
        ValidateProperty(SpentAmountText, nameof(SpentAmountText));
    }

    public void ValidateMinimumPaymentField()
    {
        _isMinimumPaymentValidationActive = true;
        ValidateProperty(MinimumPaymentText, nameof(MinimumPaymentText));
    }

    public void ValidateApyField()
    {
        _isApyValidationActive = true;
        ValidateProperty(ApyText, nameof(ApyText));
    }

    public void MarkMaximumSpendingModified()
    {
        _isMaximumSpendingModified = true;
    }

    public bool IsCredit => SelectedSpendingSourceType == SpendingSourceType.Credit;
    public bool IsBnpl => SelectedSpendingSourceType == SpendingSourceType.BNPL;
    public bool IsCreditLike => IsCredit || IsBnpl;
    public bool IsSaving => SelectedSpendingSourceType == SpendingSourceType.Saving;
    public bool IsCashLike => SelectedSpendingSourceType is SpendingSourceType.Checking or SpendingSourceType.Cash;
    public bool IsBalanceLike => IsCashLike || IsSaving;
    public string PrimaryAmountLabel => IsCreditLike ? "Current spent" : IsCashLike ? "Current amount" : "Current balance";

    partial void OnAccountLimitTextChanged(decimal value)
    {
        OnPropertyChanged(nameof(MaximumSpendingPlaceholderText));
        SyncMaximumSpendingToPlaceholder();
        RefreshActiveValidation(nameof(MaximumSpendingText), nameof(SpentAmountText));
        NotifyFormStateChanged();
    }

    partial void OnApyTextChanged(decimal value)
    {
        RefreshActiveValidation(nameof(ApyText));
        NotifyFormStateChanged();
    }

    partial void OnIsBusyChanged(bool value) => NotifyFormStateChanged();

    partial void OnIsEnabledChanged(bool value)
    {
        if (ShowOnUI != value)
            ShowOnUI = value;

        NotifyFormStateChanged();
    }

    partial void OnMaximumSpendingTextChanged(decimal value)
    {
        RefreshActiveValidation(nameof(MaximumSpendingText));
        NotifyFormStateChanged();
    }

    partial void OnMinimumPaymentTextChanged(decimal value)
    {
        RefreshActiveValidation(nameof(MinimumPaymentText));
        NotifyFormStateChanged();
    }

    partial void OnMonthlyDueDateTextChanged(string value) => NotifyFormStateChanged();

    partial void OnNameTextChanged(string value)
    {
        _isNameValidationActive = true;
        RefreshActiveValidation(nameof(NameText));
        NotifyFormStateChanged();
    }

    partial void OnPrimaryAmountTextChanged(decimal value)
    {
        OnPropertyChanged(nameof(MaximumSpendingPlaceholderText));
        SyncMaximumSpendingToPlaceholder();
        RefreshActiveValidation(nameof(MaximumSpendingText));
        NotifyFormStateChanged();
    }

    partial void OnSelectedDeductSourceChanged(int? value) => NotifyFormStateChanged();

    partial void OnShowOnUIChanged(bool value) => NotifyFormStateChanged();

    partial void OnSpentAmountTextChanged(decimal value)
    {
        RefreshActiveValidation(nameof(MaximumSpendingText), nameof(SpentAmountText));
        NotifyFormStateChanged();
    }

    partial void OnSelectedSpendingSourceTypeChanged(SpendingSourceType value)
    {
        OnPropertyChanged(nameof(IsCredit));
        OnPropertyChanged(nameof(IsBnpl));
        OnPropertyChanged(nameof(IsCreditLike));
        OnPropertyChanged(nameof(IsSaving));
        OnPropertyChanged(nameof(IsCashLike));
        OnPropertyChanged(nameof(IsBalanceLike));
        OnPropertyChanged(nameof(PrimaryAmountLabel));
        OnPropertyChanged(nameof(MaximumSpendingPlaceholderText));
        SyncMaximumSpendingToPlaceholder();

        if (!IsCreditLike)
        {
            AccountLimitText = 0m;
            SpentAmountText = 0m;
            MonthlyDueDateText = string.Empty;
            SelectedDeductSource = null;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(MonthlyDueDateText))
                MonthlyDueDateText = GetDefaultMonthlyDueDateText();

            if (!SelectedDeductSource.HasValue && DeductSources.Count > 0)
                SelectedDeductSource = DeductSources[0].Id;
        }

        if (!IsCredit)
        {
            _isMaximumSpendingModified = false;
            SyncMaximumSpendingToPlaceholder();
            MinimumPaymentText = 0m;
        }

        if (!IsSaving)
            ApyText = 0m;

        ClearValidationState();
        NotifyFormStateChanged();
    }

    public async Task LoadDeductSourcesAsync(CancellationToken cancellationToken = default)
    {
        if (_loadDraftDeductSourcesAsync is null)
            await LoadKnownSpendingSourceNamesAsync(cancellationToken);

        IReadOnlyList<DeductSourceOption> options;
        if (_loadDraftDeductSourcesAsync is not null)
        {
            options = await _loadDraftDeductSourcesAsync(EditingId);
        }
        else
        {
            var existingSources = await _appData.GetSpendingSourcesAsync(cancellationToken);
            options = existingSources
                .Where(source => source.Id != (EditingId ?? 0))
                .Where(source => source.IsEnabled)
                .Where(source => source.SpendingSourceType is not (SpendingSourceType.Credit or SpendingSourceType.BNPL))
                .OrderBy(source => source.SpendingSourceType)
                .ThenBy(source => source.Name)
                .Select(source => new DeductSourceOption(source.Id, source.Name, source.SpendingSourceType))
                .ToList();
        }

        DeductSources.Clear();
        foreach (var option in options)
            DeductSources.Add(option);

        if (!IsCreditLike)
        {
            SelectedDeductSource = null;
            return;
        }

        if (SelectedDeductSource.HasValue && DeductSources.Any(option => option.Id == SelectedDeductSource.Value))
            return;

        SelectedDeductSource = DeductSources.Count > 0 ? DeductSources[0].Id : null;
    }

    public async Task<AddSpendingSourceResult> SaveAsync()
    {
        if (IsBusy)
            return AddSpendingSourceResult.Failure("A source is already being saved.");

        if (!TryBuildInput(out var input, out var validationMessage, out var failurePresentation))
            return AddSpendingSourceResult.Failure(validationMessage, failurePresentation);

        if (_saveDraftAsync is not null)
            return await _saveDraftAsync(input);

        IsBusy = true;

        try
        {
            await LoadKnownSpendingSourceNamesAsync(CancellationToken.None);
            var existingSources = await _appData.GetSpendingSourcesAsync();
            if (HasDuplicateSourceName(input.Name))
                return AddSpendingSourceResult.Failure(
                    $"A spending source named \"{input.Name}\" already exists.");

            SpendingSource spendingSource;
            SpendingSourceMemorySnapshot? beforeSnapshot = null;
            var previousSpentAmount = 0m;

            if (EditingId.HasValue)
            {
                spendingSource = existingSources.FirstOrDefault(s => s.Id == EditingId.Value)
                                 ?? throw new InvalidOperationException("Spending source not found.");
                beforeSnapshot = SpendingSourceMemorySnapshot.Create(spendingSource);
                previousSpentAmount = spendingSource.SpentAmount;
                spendingSource.Name = input.Name;
                spendingSource.SpendingSourceType = input.SpendingSourceType;
                spendingSource.AccountLimit = input.AccountLimit;
                spendingSource.SpentAmount = input.SpentAmount;
                spendingSource.Balance = input.Balance;
                spendingSource.MaximumSpending = input.MaximumSpending;
                spendingSource.MinimumPayment = input.MinimumPayment;
                spendingSource.MonthlyDueDate = input.MonthlyDueDate;
                spendingSource.DeductSource = input.DeductSource;
                spendingSource.InterestRate = input.InterestRate;
                spendingSource.ShowOnUI = ResolveShowOnUiFromEnabledState(spendingSource.IsEnabled, input.IsEnabled, input.ShowOnUI);
                spendingSource.IsEnabled = input.IsEnabled;
                _appData.UpdateSpendingSource(spendingSource);
            }
            else
            {
                spendingSource = new SpendingSource
                {
                    Name = input.Name,
                    SpendingSourceType = input.SpendingSourceType,
                    AccountLimit = input.AccountLimit,
                    MaximumSpending = input.MaximumSpending,
                    MinimumPayment = input.MinimumPayment,
                    SpentAmount = input.SpentAmount,
                    Balance = input.Balance,
                    MonthlyDueDate = input.MonthlyDueDate,
                    DeductSource = input.DeductSource,
                    InterestRate = input.InterestRate,
                    ShowOnUI = ResolveShowOnUiForCreation(input.IsEnabled, input.ShowOnUI),
                    IsEnabled = input.IsEnabled
                };
                await _appData.AddSpendingSourceAsync(spendingSource);
            }

            await _appData.SaveChangesAsync();

            if (!EditingId.HasValue &&
                spendingSource.SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL &&
                spendingSource.MonthlyDueDate.HasValue &&
                spendingSource.DeductSource.HasValue)
            {
                var paymentTag = await ResolvePaymentOrTransferTagAsync(_appData);
                await _appData.AddRecurringTransactionAsync(new RecurringTransaction
                {
                    Name = $"Payment to {spendingSource.Name}",
                    Amount = Math.Max(spendingSource.SpentAmount, 0m),
                    RecurringPeriod = RecurringPeriod.Monthly,
                    RecurringTime = spendingSource.MonthlyDueDate.Value,
                    Type = RecurringTransactionType.Expense,
                    SourceId = spendingSource.DeductSource.Value,
                    TagId = paymentTag?.Id,
                    GoalId = null,
                    IsEnabled = true
                });
                await _appData.SaveChangesAsync();
            }

            var afterSnapshot = SpendingSourceMemorySnapshot.Create(spendingSource);
            var autoTransactionSnapshot = await TryCreateBalanceUpdateTransactionAsync(
                _appData,
                spendingSource,
                previousSpentAmount,
                input.SpentAmount,
                EditingId.HasValue);

            ILogMemoryAction sourceAction = EditingId.HasValue
                ? new EditSpendingSourceMemoryAction(beforeSnapshot, afterSnapshot)
                : new AddSpendingSourceMemoryAction(afterSnapshot);

            ILogMemoryAction historyAction = autoTransactionSnapshot is null
                ? sourceAction
                : new CompositeLogMemoryAction(
                    sourceAction.Description,
                    [
                        sourceAction,
                        new AddExpenseLogMemoryAction(
                            autoTransactionSnapshot,
                            shouldAdjustSpendingSourceTotals: false)
                    ]);

            WeakReferenceMessenger.Default.Send(new RecordLogMemoryMessage(historyAction));
            WeakReferenceMessenger.Default.Send(new DashboardDataInvalidatedMessage(
                DashboardDataInvalidationScope.Budget | DashboardDataInvalidationScope.Notifications));

            await _mainViewModel.ReloadCurrentDataAsync();
            return AddSpendingSourceResult.Success(true);
        }
        catch (Exception exception)
        {
            FluxoLogManager.LogError(exception, "Unable to create this spending source.");
            return AddSpendingSourceResult.Failure(
                FluxoLogManager.CreateFailureMessage("create spending source"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static decimal CalculateBalanceUpdateAmount(
        SpendingSourceType sourceType,
        bool isEdit,
        decimal currentSpentAmount,
        decimal previousSpentAmount)
    {
        if (sourceType != SpendingSourceType.Credit)
            return 0m;

        return isEdit
            ? currentSpentAmount - previousSpentAmount
            : currentSpentAmount;
    }

    private static async Task<ExpenseTag> ResolveBalanceUpdateTagAsync(IAppDataService appData)
    {
        var tags = await appData.GetExpenseTagsAsync();
        var existingTag = tags.FirstOrDefault(tag =>
            string.Equals(tag.Name, BalanceUpdateTagName, StringComparison.OrdinalIgnoreCase));
        if (existingTag is not null)
            return existingTag;

        var balanceUpdateTag = new ExpenseTag
        {
            Name = BalanceUpdateTagName,
            HexCode = BalanceUpdateTagColor
        };

        await appData.AddExpenseTagAsync(balanceUpdateTag);
        await appData.SaveChangesAsync();
        return balanceUpdateTag;
    }

    private static async Task<ExpenseTag?> ResolvePaymentOrTransferTagAsync(IAppDataService appData)
    {
        var tags = await appData.GetExpenseTagsAsync();
        return tags
            .OrderByDescending(tag => string.Equals(tag.Name, "Payment", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(tag => string.Equals(tag.Name, "Transfer", StringComparison.OrdinalIgnoreCase))
            .ThenBy(tag => tag.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static async Task<ExpenseLogMemorySnapshot?> TryCreateBalanceUpdateTransactionAsync(
        IAppDataService appData,
        SpendingSource spendingSource,
        decimal previousSpentAmount,
        decimal currentSpentAmount,
        bool isEdit)
    {
        var triggerAmount = CalculateBalanceUpdateAmount(
            spendingSource.SpendingSourceType,
            isEdit,
            currentSpentAmount,
            previousSpentAmount);
        if (triggerAmount <= 0m)
            return null;

        var balanceUpdateTag = await ResolveBalanceUpdateTagAsync(appData);
        var expense = new Expense
        {
            Name = $"Balance Update",
            Amount = triggerAmount,
            ExpenseCategory = ExpenseCategory.Needs,
            SpendingSourceId = spendingSource.Id,
            ExpenseTagId = balanceUpdateTag.Id
        };

        await appData.AddExpenseAsync(expense);

        var expenseLog = new ExpenseLog
        {
            Expense = expense,
            SpendingSourceId = spendingSource.Id,
            Amount = triggerAmount,
            DeductedOn = DateTime.Now,
            Notes = string.Empty,
            IsForDeletion = false
        };

        await appData.AddExpenseLogAsync(expenseLog);
        await appData.SaveChangesAsync();

        return new ExpenseLogMemorySnapshot(
            expense.Id,
            expenseLog.Id,
            expense.Name,
            expense.Amount,
            expense.ExpenseCategory,
            spendingSource.Id,
            balanceUpdateTag.Id,
            expenseLog.DeductedOn,
            expenseLog.Notes,
            expenseLog.IsForDeletion);
    }

    private bool TryBuildInput(
        out AddSpendingSourceInput input,
        out string validationMessage,
        out AddSpendingSourceFailurePresentation failurePresentation)
    {
        input = default;
        validationMessage = string.Empty;
        failurePresentation = AddSpendingSourceFailurePresentation.Dialog;

        var name = (NameText ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            validationMessage = "Please enter a source name.";
            return false;
        }

        if (name.Length > MaxNameLength)
        {
            validationMessage = $"Source name cannot exceed {MaxNameLength} characters.";
            return false;
        }

        if (name.Any(char.IsControl))
        {
            validationMessage = "Source name cannot contain control characters.";
            return false;
        }

        var primaryAmount = PrimaryAmountText;
        var spentAmount = SpentAmountText;
        var accountLimit = AccountLimitText;
        var maximumSpending = GetEffectiveMaximumSpending();
        var minimumPayment = IsCredit ? MinimumPaymentText : 0m;
        decimal? interestRate = IsSaving ? ApyText : null;

        if (primaryAmount < 0m || spentAmount < 0m || accountLimit < 0m ||
            maximumSpending < 0m || minimumPayment < 0m ||
            (interestRate.HasValue && interestRate.Value < 0m))
        {
            validationMessage = "Values must be zero or greater.";
            return false;
        }

        if (SelectedSpendingSourceType == SpendingSourceType.Credit && accountLimit <= 0m)
        {
            validationMessage = "Credit sources require an account limit greater than zero.";
            return false;
        }

        if (IsCredit && spentAmount > accountLimit)
        {
            validationMessage = "Spent amount cannot exceed the account limit.";
            failurePresentation = AddSpendingSourceFailurePresentation.ToastWarning;
            return false;
        }

        if (IsCredit && minimumPayment <= 0m)
        {
            validationMessage = "Minimum payment must be greater than zero.";
            return false;
        }

        if (IsSaving && interestRate.HasValue && interestRate.Value <= 0m)
        {
            validationMessage = "APY must be greater than zero.";
            return false;
        }

        if (IsSaving && interestRate.HasValue && interestRate.Value > PercentageMaximum)
        {
            validationMessage = "APY cannot exceed 100%.";
            failurePresentation = AddSpendingSourceFailurePresentation.ToastWarning;
            return false;
        }

        if (IsCredit && minimumPayment > PercentageMaximum)
        {
            validationMessage = "Minimum payment cannot exceed 100%.";
            failurePresentation = AddSpendingSourceFailurePresentation.ToastWarning;
            return false;
        }

        if (IsCredit && maximumSpending > 0m)
        {
            if (maximumSpending > accountLimit)
            {
                validationMessage = "Maximum spending cannot exceed the account limit.";
                failurePresentation = AddSpendingSourceFailurePresentation.ToastWarning;
                return false;
            }
        }

        if (SelectedSpendingSourceType is SpendingSourceType.Checking or SpendingSourceType.Cash &&
            maximumSpending > primaryAmount)
        {
            validationMessage = "Maximum spending cannot exceed the current balance.";
            failurePresentation = AddSpendingSourceFailurePresentation.ToastWarning;
            return false;
        }

        int? monthlyDueDate = null;
        if (IsCreditLike)
        {
            if (string.IsNullOrWhiteSpace(MonthlyDueDateText))
            {
                validationMessage = "Credit and BNPL sources require a due date.";
                return false;
            }

            if (!TryParseMonthlyDueDate(MonthlyDueDateText, out var parsedMonthlyDueDate))
            {
                validationMessage = "Due date must be a number between 1 and 28.";
                return false;
            }

            monthlyDueDate = parsedMonthlyDueDate;

            if (!SelectedDeductSource.HasValue || DeductSources.All(option => option.Id != SelectedDeductSource.Value))
            {
                validationMessage = "Credit and BNPL sources require a deduct source.";
                return false;
            }
        }

        input = new AddSpendingSourceInput(
            name,
            SelectedSpendingSourceType,
            IsCreditLike ? 0m : primaryAmount,
            IsCreditLike ? spentAmount : 0m,
            IsCredit ? accountLimit : 0m,
            maximumSpending,
            IsCredit ? minimumPayment : null,
            monthlyDueDate,
            IsCreditLike ? SelectedDeductSource : null,
            interestRate,
            ShowOnUI,
            IsEnabled);

        return true;
    }

    private static bool TryParseMonthlyDueDate(string text, out int monthlyDueDate)
    {
        monthlyDueDate = 0;
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out monthlyDueDate) &&
               monthlyDueDate is >= MonthlyDueDateHelper.MinMonthlyDay and <= MonthlyDueDateHelper.MaxMonthlyDay;
    }

    private static string GetDefaultMonthlyDueDateText()
    {
        return MonthlyDueDateHelper.Normalize(DateTime.Today.Day)?.ToString(CultureInfo.InvariantCulture) ??
               MonthlyDueDateHelper.MinMonthlyDay.ToString(CultureInfo.InvariantCulture);
    }

    private async Task LoadKnownSpendingSourceNamesAsync(CancellationToken cancellationToken)
    {
        _knownSpendingSourceNames.Clear();

        var existingSources = await _appData.GetSpendingSourcesAsync(cancellationToken);
        foreach (var source in existingSources)
        {
            if (source.Id == (EditingId ?? int.MinValue))
                continue;

            var normalizedName = NormalizeSourceName(source.Name);
            if (normalizedName.Length > 0)
                _knownSpendingSourceNames.Add(normalizedName);
        }
    }

    private bool HasDuplicateSourceName(string name)
    {
        var normalizedName = NormalizeSourceName(name);
        return normalizedName.Length > 0 && _knownSpendingSourceNames.Contains(normalizedName);
    }

    private static string NormalizeSourceName(string? value)
    {
        return (value ?? string.Empty).Trim();
    }

    private static bool ResolveShowOnUiForCreation(bool isEnabled, bool requestedShowOnUi)
    {
        return isEnabled && requestedShowOnUi;
    }

    private static bool ResolveShowOnUiFromEnabledState(bool previousIsEnabled, bool nextIsEnabled, bool requestedShowOnUi)
    {
        if (!nextIsEnabled)
            return false;

        if (previousIsEnabled == nextIsEnabled)
            return requestedShowOnUi;

        return true;
    }

    private bool AreRequiredFieldsFilled()
    {
        if (!HasValidNameValue(NameText))
            return false;

        if (IsCredit && AccountLimitText <= 0m)
            return false;

        if (IsCreditLike && !TryParseMonthlyDueDate(MonthlyDueDateText, out _))
            return false;

        if (IsCreditLike && !SelectedDeductSource.HasValue)
            return false;

        return true;
    }

    private static bool HasValidNameValue(string? value)
    {
        var trimmedName = value?.Trim() ?? string.Empty;
        return trimmedName.Length > 0 &&
               trimmedName.Length <= MaxNameLength &&
               !trimmedName.Any(char.IsControl);
    }

    private void RefreshActiveValidation(params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (propertyName == nameof(NameText) && _isNameValidationActive)
                ValidateProperty(NameText, nameof(NameText));

            if (propertyName == nameof(MaximumSpendingText) && _isMaximumSpendingValidationActive)
                ValidateProperty(MaximumSpendingText, nameof(MaximumSpendingText));

            if (propertyName == nameof(SpentAmountText) && _isSpentAmountValidationActive)
                ValidateProperty(SpentAmountText, nameof(SpentAmountText));

            if (propertyName == nameof(MinimumPaymentText) && _isMinimumPaymentValidationActive)
                ValidateProperty(MinimumPaymentText, nameof(MinimumPaymentText));

            if (propertyName == nameof(ApyText) && _isApyValidationActive)
                ValidateProperty(ApyText, nameof(ApyText));
        }
    }

    private void ResetValidationState()
    {
        _isMaximumSpendingModified = false;
        ClearValidationState();
    }

    private void ClearValidationState()
    {
        _isApyValidationActive = false;
        _isMaximumSpendingValidationActive = false;
        _isMinimumPaymentValidationActive = false;
        _isNameValidationActive = false;
        _isSpentAmountValidationActive = false;
        ClearErrors();
        OnPropertyChanged(nameof(NameValidationHint));
        OnPropertyChanged(nameof(MaximumSpendingValidationHint));
        OnPropertyChanged(nameof(SpentAmountValidationHint));
        OnPropertyChanged(nameof(MinimumPaymentValidationHint));
        OnPropertyChanged(nameof(ApyValidationHint));
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
            nameof(MaximumSpendingText) => "Exceeds Limit",
            nameof(SpentAmountText) => "Exceeds Limit",
            nameof(MinimumPaymentText) => message.Contains("greater than zero", StringComparison.OrdinalIgnoreCase)
                ? "Required"
                : "Invalid Rate",
            nameof(ApyText) => message.Contains("greater than zero", StringComparison.OrdinalIgnoreCase)
                ? "Required"
                : "Invalid Rate",
            _ => string.Empty
        };
    }

    private static string GetNameValidationHint(string message)
    {
        if (message.Contains("enter a source name", StringComparison.OrdinalIgnoreCase))
            return "Required";

        if (message.Contains("exceed", StringComparison.OrdinalIgnoreCase))
            return "Too Long";

        return "Invalid Name";
    }

    private decimal GetEffectiveMaximumSpending()
    {
        return MaximumSpendingText;
    }

    private void SyncMaximumSpendingToPlaceholder()
    {
        if (!_isMaximumSpendingModified)
            MaximumSpendingText = GetMaximumSpendingPlaceholderValue();
    }

    private decimal GetMaximumSpendingPlaceholderValue()
    {
        return SelectedSpendingSourceType switch
        {
            SpendingSourceType.Credit => AccountLimitText,
            SpendingSourceType.Checking or SpendingSourceType.Cash => PrimaryAmountText,
            _ => 0m
        };
    }

    private static string FormatPlaceholderAmount(decimal value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private FormState CaptureState()
    {
        return new FormState(
            NameText ?? string.Empty,
            PrimaryAmountText,
            SpentAmountText,
            AccountLimitText,
            MinimumPaymentText,
            ApyText,
            ShowOnUI,
            IsEnabled);
    }

    private void NotifyFormStateChanged()
    {
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(HasChanges));
    }

    public static ValidationResult? ValidateNameText(string value, ValidationContext validationContext)
    {
        var viewModel = (AddSpendingSourceVM)validationContext.ObjectInstance;
        var trimmedName = value?.Trim() ?? string.Empty;

        if (trimmedName.Length == 0)
            return new ValidationResult("Please enter a source name.");

        if (trimmedName.Length > MaxNameLength)
            return new ValidationResult($"Source name cannot exceed {MaxNameLength} characters.");

        if (trimmedName.Any(char.IsControl))
            return new ValidationResult("Source name cannot contain control characters.");

        if (viewModel.HasDuplicateSourceName(trimmedName))
            return new ValidationResult($"A spending source named \"{trimmedName}\" already exists.");

        return ValidationResult.Success;
    }

    public static ValidationResult? ValidateMaximumSpendingText(decimal value, ValidationContext validationContext)
    {
        var viewModel = (AddSpendingSourceVM)validationContext.ObjectInstance;
        if (value < 0m)
            return new ValidationResult("Maximum spending must be zero or greater.");

        if (value == 0m)
            return ValidationResult.Success;

        if (viewModel.SelectedSpendingSourceType is SpendingSourceType.Checking or SpendingSourceType.Cash &&
            value > viewModel.PrimaryAmountText)
            return new ValidationResult("Maximum spending cannot exceed the current balance.");

        if (viewModel.SelectedSpendingSourceType == SpendingSourceType.Credit &&
            value > viewModel.AccountLimitText)
            return new ValidationResult("Maximum spending cannot exceed the account limit.");

        return ValidationResult.Success;
    }

    public static ValidationResult? ValidateSpentAmountText(decimal value, ValidationContext validationContext)
    {
        var viewModel = (AddSpendingSourceVM)validationContext.ObjectInstance;
        if (value < 0m)
            return new ValidationResult("Spent amount must be zero or greater.");

        return viewModel.SelectedSpendingSourceType == SpendingSourceType.Credit &&
               viewModel.AccountLimitText > 0m &&
               value > viewModel.AccountLimitText
            ? new ValidationResult("Spent amount cannot exceed the account limit.")
            : ValidationResult.Success;
    }

    public static ValidationResult? ValidateMinimumPaymentText(decimal value, ValidationContext validationContext)
    {
        var viewModel = (AddSpendingSourceVM)validationContext.ObjectInstance;
        if (!viewModel.IsCredit)
            return ValidationResult.Success;

        if (value <= 0m)
            return new ValidationResult("Minimum payment must be greater than zero.");

        return value > PercentageMaximum
            ? new ValidationResult("Minimum payment cannot exceed 100%.")
            : ValidationResult.Success;
    }

    public static ValidationResult? ValidateApyText(decimal value, ValidationContext validationContext)
    {
        var viewModel = (AddSpendingSourceVM)validationContext.ObjectInstance;
        if (!viewModel.IsSaving)
            return ValidationResult.Success;

        if (value <= 0m)
            return new ValidationResult("APY must be greater than zero.");

        return value > PercentageMaximum
            ? new ValidationResult("APY cannot exceed 100%.")
            : ValidationResult.Success;
    }

    public enum AddSpendingSourceFailurePresentation
    {
        Dialog,
        ToastWarning
    }

    public readonly record struct AddSpendingSourceResult(
        bool IsSuccess,
        bool ShouldClose,
        string? ErrorMessage,
        AddSpendingSourceFailurePresentation FailurePresentation = AddSpendingSourceFailurePresentation.Dialog)
    {
        public static AddSpendingSourceResult Success(bool shouldClose = false)
        {
            return new AddSpendingSourceResult(true, shouldClose, null);
        }

        public static AddSpendingSourceResult Failure(
            string? errorMessage,
            AddSpendingSourceFailurePresentation failurePresentation = AddSpendingSourceFailurePresentation.Dialog)
        {
            return new AddSpendingSourceResult(false, false, errorMessage, failurePresentation);
        }
    }

    public readonly record struct SpendingSourceTypeOption(string Label, SpendingSourceType Value);

    public readonly record struct AddSpendingSourceInput(
        string Name,
        SpendingSourceType SpendingSourceType,
        decimal Balance,
        decimal SpentAmount,
        decimal AccountLimit,
        decimal MaximumSpending,
        decimal? MinimumPayment,
        int? MonthlyDueDate,
        int? DeductSource,
        decimal? InterestRate,
        bool ShowOnUI,
        bool IsEnabled);

    private readonly record struct FormState(
        string NameText,
        decimal PrimaryAmountText,
        decimal SpentAmountText,
        decimal AccountLimitText,
        decimal MinimumPaymentText,
        decimal ApyText,
        bool ShowOnUI,
        bool IsEnabled);

    public readonly record struct DeductSourceOption(
        int Id,
        string Name,
        SpendingSourceType SpendingSourceType = SpendingSourceType.Checking)
    {
        public string TypeDisplayName => SpendingSourceType switch
        {
            SpendingSourceType.Credit => "Credit",
            SpendingSourceType.BNPL => "BNPL",
            SpendingSourceType.Checking => "Checking",
            SpendingSourceType.Cash => "Cash",
            SpendingSourceType.Saving => "Savings",
            _ => "Source"
        };
    }
}
