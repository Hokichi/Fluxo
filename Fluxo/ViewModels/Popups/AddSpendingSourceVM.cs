using System.Globalization;
using System.Collections.ObjectModel;
using System.ComponentModel;
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

public partial class AddSpendingSourceVM : ObservableObject
{
    private const string BalanceUpdateTagColor = "#e8ca5f";
    private const string BalanceUpdateTagName = "Balance Update";

    private readonly MainVM _mainViewModel;
    private readonly IAppDataService _appData;
    private readonly Func<AddSpendingSourceInput, Task<AddSpendingSourceResult>>? _saveDraftAsync;
    private readonly Func<int?, Task<IReadOnlyList<DeductSourceOption>>>? _loadDraftDeductSourcesAsync;
    private FormState _initialState;
    private bool _isChangeTrackingInitialized;

    [ObservableProperty] private decimal _accountLimitText;
    [ObservableProperty] private decimal _apyText;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isEnabled = true;
    [ObservableProperty] private decimal _maximumSpendingText;
    [ObservableProperty] private decimal _minimumPaymentText;
    [ObservableProperty] private string _monthlyDueDateText = string.Empty;
    [ObservableProperty] private string _nameText = string.Empty;
    [ObservableProperty] private decimal _primaryAmountText;
    [ObservableProperty] private int? _selectedDeductSource;
    [ObservableProperty] private SpendingSourceType _selectedSpendingSourceType = SpendingSourceType.Checking;
    [ObservableProperty] private bool _showOnUI = true;
    [ObservableProperty] private decimal _spentAmountText;

    public int? EditingId { get; init; }

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

    public bool CanSave => !IsBusy && AreRequiredFieldsFilled();
    public bool HasChanges => _isChangeTrackingInitialized && !CaptureState().Equals(_initialState);
    public bool IsEditMode => EditingId.HasValue;
    public string PopupTitle => IsEditMode ? "Edit Income Source" : "Add New Income Source";
    public string HeaderTitle => PopupTitle;

    public string HeaderDescription => IsEditMode
        ? "Update this source for checking, cash, credit, BNPL, or savings."
        : "Set up a new source for checking, cash, credit, BNPL, or savings.";

    public string ValidationDialogTitle => PopupTitle;

    public void BeginChangeTracking()
    {
        _initialState = CaptureState();
        _isChangeTrackingInitialized = true;
        NotifyFormStateChanged();
    }

    public bool IsCredit => SelectedSpendingSourceType == SpendingSourceType.Credit;
    public bool IsBnpl => SelectedSpendingSourceType == SpendingSourceType.BNPL;
    public bool IsCreditLike => IsCredit || IsBnpl;
    public bool IsSaving => SelectedSpendingSourceType == SpendingSourceType.Saving;
    public bool IsCashLike => SelectedSpendingSourceType is SpendingSourceType.Checking or SpendingSourceType.Cash;
    public bool IsBalanceLike => IsCashLike || IsSaving;
    public string PrimaryAmountLabel => IsCreditLike ? "Current spent" : IsCashLike ? "Current amount" : "Current balance";

    partial void OnAccountLimitTextChanged(decimal value) => NotifyFormStateChanged();

    partial void OnApyTextChanged(decimal value) => NotifyFormStateChanged();

    partial void OnIsBusyChanged(bool value) => NotifyFormStateChanged();

    partial void OnIsEnabledChanged(bool value) => NotifyFormStateChanged();

    partial void OnMaximumSpendingTextChanged(decimal value) => NotifyFormStateChanged();

    partial void OnMinimumPaymentTextChanged(decimal value) => NotifyFormStateChanged();

    partial void OnMonthlyDueDateTextChanged(string value) => NotifyFormStateChanged();

    partial void OnNameTextChanged(string value) => NotifyFormStateChanged();

    partial void OnPrimaryAmountTextChanged(decimal value) => NotifyFormStateChanged();

    partial void OnSelectedDeductSourceChanged(int? value) => NotifyFormStateChanged();

    partial void OnShowOnUIChanged(bool value) => NotifyFormStateChanged();

    partial void OnSpentAmountTextChanged(decimal value) => NotifyFormStateChanged();

    partial void OnSelectedSpendingSourceTypeChanged(SpendingSourceType value)
    {
        OnPropertyChanged(nameof(IsCredit));
        OnPropertyChanged(nameof(IsBnpl));
        OnPropertyChanged(nameof(IsCreditLike));
        OnPropertyChanged(nameof(IsSaving));
        OnPropertyChanged(nameof(IsCashLike));
        OnPropertyChanged(nameof(IsBalanceLike));
        OnPropertyChanged(nameof(PrimaryAmountLabel));

        if (!IsCreditLike)
        {
            AccountLimitText = 0m;
            SpentAmountText = 0m;
            MonthlyDueDateText = string.Empty;
            SelectedDeductSource = null;
        }
        else if (!SelectedDeductSource.HasValue && DeductSources.Count > 0)
        {
            SelectedDeductSource = DeductSources[0].Id;
        }

        if (!IsCredit)
            MinimumPaymentText = 0m;

        if (!IsSaving)
            ApyText = 0m;

        NotifyFormStateChanged();
    }

    public async Task LoadDeductSourcesAsync(CancellationToken cancellationToken = default)
    {
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

        if (!TryBuildInput(out var input, out var validationMessage))
            return AddSpendingSourceResult.Failure(validationMessage);

        if (_saveDraftAsync is not null)
            return await _saveDraftAsync(input);

        IsBusy = true;

        try
        {
            var existingSources = await _appData.GetSpendingSourcesAsync();
            if (existingSources.Any(source =>
                    source.Id != (EditingId ?? -1) &&
                    string.Equals(source.Name, input.Name, StringComparison.OrdinalIgnoreCase)))
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
                spendingSource.ShowOnUI = input.ShowOnUI;
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
                    ShowOnUI = input.ShowOnUI,
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

    private bool TryBuildInput(out AddSpendingSourceInput input, out string validationMessage)
    {
        input = default;
        validationMessage = string.Empty;

        var name = (NameText ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            validationMessage = "Please enter a source name.";
            return false;
        }

        var primaryAmount = PrimaryAmountText;
        var spentAmount = SpentAmountText;
        var accountLimit = AccountLimitText;
        var maximumSpending = MaximumSpendingText;
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

    private bool AreRequiredFieldsFilled()
    {
        if (string.IsNullOrWhiteSpace(NameText))
            return false;

        if (IsCredit && AccountLimitText <= 0m)
            return false;

        if (IsCreditLike && !TryParseMonthlyDueDate(MonthlyDueDateText, out _))
            return false;

        if (IsCreditLike && !SelectedDeductSource.HasValue)
            return false;

        return true;
    }

    private FormState CaptureState()
    {
        return new FormState(
            NameText ?? string.Empty,
            PrimaryAmountText,
            SpentAmountText,
            AccountLimitText,
            MaximumSpendingText,
            MinimumPaymentText,
            ApyText,
            MonthlyDueDateText ?? string.Empty,
            SelectedDeductSource,
            SelectedSpendingSourceType,
            ShowOnUI,
            IsEnabled);
    }

    private void NotifyFormStateChanged()
    {
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(HasChanges));
    }

    public readonly record struct AddSpendingSourceResult(bool IsSuccess, bool ShouldClose, string? ErrorMessage)
    {
        public static AddSpendingSourceResult Success(bool shouldClose = false)
        {
            return new AddSpendingSourceResult(true, shouldClose, null);
        }

        public static AddSpendingSourceResult Failure(string? errorMessage)
        {
            return new AddSpendingSourceResult(false, false, errorMessage);
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
        decimal MaximumSpendingText,
        decimal MinimumPaymentText,
        decimal ApyText,
        string MonthlyDueDateText,
        int? SelectedDeductSource,
        SpendingSourceType SpendingSourceType,
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
