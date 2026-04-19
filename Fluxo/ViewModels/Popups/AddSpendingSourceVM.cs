using System.Globalization;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
using Fluxo.Resources.Messages;
using Fluxo.Services.History;
using Fluxo.ViewModels.Helpers;
using Fluxo.ViewModels.Shell;

namespace Fluxo.ViewModels.Popups;

public partial class AddSpendingSourceVM : ObservableObject
{
    private const string BalanceUpdateTagColor = "#e8ca5f";
    private const string BalanceUpdateTagName = "Balance Update";

    private readonly MainVM _mainViewModel;
    private readonly IUnitOfWork _unitOfWork;
    private FormState _initialState;
    private bool _isChangeTrackingInitialized;

    [ObservableProperty] private string _accountLimitText = string.Empty;
    [ObservableProperty] private string _apyText = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isEnabled = true;
    [ObservableProperty] private string _monthlyDueDateText = string.Empty;
    [ObservableProperty] private string _nameText = string.Empty;
    [ObservableProperty] private string _primaryAmountText = string.Empty;
    [ObservableProperty] private int? _selectedDeductSource;
    [ObservableProperty] private SpendingSourceType _selectedSpendingSourceType = SpendingSourceType.Checking;
    [ObservableProperty] private bool _showOnUI = true;
    [ObservableProperty] private string _spentAmountText = string.Empty;

    public int? EditingId { get; init; }

    public AddSpendingSourceVM(MainVM mainViewModel, IUnitOfWork unitOfWork)
    {
        _mainViewModel = mainViewModel;
        _unitOfWork = unitOfWork;
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

    public bool CanSave => !IsBusy && AreRequiredFieldsFilled();
    public bool HasChanges => _isChangeTrackingInitialized && !CaptureState().Equals(_initialState);

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
    public string PrimaryAmountLabel => IsCreditLike ? "Current spent" : IsCashLike ? "Current amount" : "Current balance";

    partial void OnAccountLimitTextChanged(string value) => NotifyFormStateChanged();

    partial void OnApyTextChanged(string value) => NotifyFormStateChanged();

    partial void OnIsBusyChanged(bool value) => NotifyFormStateChanged();

    partial void OnIsEnabledChanged(bool value) => NotifyFormStateChanged();

    partial void OnMonthlyDueDateTextChanged(string value) => NotifyFormStateChanged();

    partial void OnNameTextChanged(string value) => NotifyFormStateChanged();

    partial void OnPrimaryAmountTextChanged(string value) => NotifyFormStateChanged();
    partial void OnSelectedDeductSourceChanged(int? value) => NotifyFormStateChanged();

    partial void OnShowOnUIChanged(bool value) => NotifyFormStateChanged();

    partial void OnSpentAmountTextChanged(string value) => NotifyFormStateChanged();

    partial void OnSelectedSpendingSourceTypeChanged(SpendingSourceType value)
    {
        OnPropertyChanged(nameof(IsCredit));
        OnPropertyChanged(nameof(IsBnpl));
        OnPropertyChanged(nameof(IsCreditLike));
        OnPropertyChanged(nameof(IsSaving));
        OnPropertyChanged(nameof(IsCashLike));
        OnPropertyChanged(nameof(PrimaryAmountLabel));

        if (!IsCreditLike)
        {
            AccountLimitText = string.Empty;
            SpentAmountText = string.Empty;
            MonthlyDueDateText = string.Empty;
            SelectedDeductSource = null;
        }
        else if (!SelectedDeductSource.HasValue && DeductSources.Count > 0)
        {
            SelectedDeductSource = DeductSources[0].Id;
        }

        if (!IsSaving)
            ApyText = string.Empty;

        NotifyFormStateChanged();
    }

    public async Task LoadDeductSourcesAsync(CancellationToken cancellationToken = default)
    {
        var existingSources = await _unitOfWork.SpendingSources.GetAllAsync(cancellationToken);

        var options = existingSources
            .Where(source => source.Id != (EditingId ?? 0))
            .Where(source => source.SpendingSourceType is not (SpendingSourceType.Credit or SpendingSourceType.BNPL))
            .OrderBy(source => source.Name)
            .Select(source => new DeductSourceOption(source.Id, source.Name))
            .ToList();

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

        IsBusy = true;

        try
        {
            var unitOfWork = _unitOfWork;

            var existingSources = await unitOfWork.SpendingSources.GetAllAsync();
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
                spendingSource.MonthlyDueDate = input.MonthlyDueDate;
                spendingSource.DeductSource = input.DeductSource;
                spendingSource.InterestRate = input.InterestRate;
                spendingSource.ShowOnUI = input.ShowOnUI;
                spendingSource.IsEnabled = input.IsEnabled;
                unitOfWork.SpendingSources.Update(spendingSource);
            }
            else
            {
                spendingSource = new SpendingSource
                {
                    Name = input.Name,
                    SpendingSourceType = input.SpendingSourceType,
                    AccountLimit = input.AccountLimit,
                    SpentAmount = input.SpentAmount,
                    Balance = input.Balance,
                    MonthlyDueDate = input.MonthlyDueDate,
                    DeductSource = input.DeductSource,
                    InterestRate = input.InterestRate,
                    ShowOnUI = input.ShowOnUI,
                    IsEnabled = input.IsEnabled
                };
                await unitOfWork.SpendingSources.AddAsync(spendingSource);
            }

            await unitOfWork.SaveChangesAsync();
            var afterSnapshot = SpendingSourceMemorySnapshot.Create(spendingSource);
            var autoTransactionSnapshot = await TryCreateBalanceUpdateTransactionAsync(
                unitOfWork,
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
            return AddSpendingSourceResult.Failure(
                $"Unable to create this spending source.\n\n{exception.Message}");
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

    private static async Task<ExpenseTag> ResolveBalanceUpdateTagAsync(IUnitOfWork unitOfWork)
    {
        var tags = await unitOfWork.ExpenseTags.GetAllAsync();
        var existingTag = tags.FirstOrDefault(tag =>
            string.Equals(tag.Name, BalanceUpdateTagName, StringComparison.OrdinalIgnoreCase));
        if (existingTag is not null)
            return existingTag;

        var balanceUpdateTag = new ExpenseTag
        {
            Name = BalanceUpdateTagName,
            HexCode = BalanceUpdateTagColor,
            IconName = string.Empty
        };

        await unitOfWork.ExpenseTags.AddAsync(balanceUpdateTag);
        await unitOfWork.SaveChangesAsync();
        return balanceUpdateTag;
    }

    private static async Task<ExpenseLogMemorySnapshot?> TryCreateBalanceUpdateTransactionAsync(
        IUnitOfWork unitOfWork,
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

        var balanceUpdateTag = await ResolveBalanceUpdateTagAsync(unitOfWork);
        var expense = new Expense
        {
            Name = $"Balance Update For {spendingSource.Name}",
            Amount = triggerAmount,
            ExpenseKind = ExpenseKind.Manual,
            ExpenseCategory = ExpenseCategory.Needs,
            RecurringDate = DateTime.Today.Day,
            IsActive = false,
            SpendingSourceId = spendingSource.Id,
            ExpenseTagId = balanceUpdateTag.Id
        };

        await unitOfWork.Expenses.AddAsync(expense);

        var expenseLog = new ExpenseLog
        {
            Expense = expense,
            SpendingSourceId = spendingSource.Id,
            Amount = triggerAmount,
            DeductedOn = DateTime.Now,
            Notes = string.Empty,
            IsForDeletion = false
        };

        await unitOfWork.ExpenseLogs.AddAsync(expenseLog);
        await unitOfWork.SaveChangesAsync();

        return new ExpenseLogMemorySnapshot(
            expense.Id,
            expenseLog.Id,
            expense.Name,
            expense.Amount,
            expense.ExpenseKind,
            expense.ExpenseCategory,
            expense.RecurringDate,
            expense.IsActive,
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

        if (!TryParseDecimal(PrimaryAmountText, out var primaryAmount))
        {
            validationMessage = $"{PrimaryAmountLabel} must be a valid amount.";
            return false;
        }

        if (!TryParseDecimal(SpentAmountText, out var spentAmount))
        {
            validationMessage = "Current spent must be a valid amount.";
            return false;
        }

        if (!TryParseDecimal(AccountLimitText, out var accountLimit))
        {
            validationMessage = "Account limit must be a valid amount.";
            return false;
        }

        decimal? interestRate = null;
        if (!string.IsNullOrWhiteSpace(ApyText))
        {
            if (!TryParseDecimal(ApyText, out var parsedApy))
            {
                validationMessage = "APY must be a valid amount.";
                return false;
            }

            interestRate = parsedApy;
        }

        if (primaryAmount < 0m || spentAmount < 0m || accountLimit < 0m || interestRate < 0m)
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
            monthlyDueDate,
            IsCreditLike ? SelectedDeductSource : null,
            IsSaving ? interestRate : null,
            ShowOnUI,
            IsEnabled);

        return true;
    }

    private static bool TryParseDecimal(string text, out decimal value)
    {
        value = 0m;
        var normalizedText = (text ?? string.Empty)
            .Trim()
            .Replace(CultureInfo.CurrentCulture.NumberFormat.CurrencySymbol, string.Empty, StringComparison.Ordinal)
            .Replace(",", string.Empty, StringComparison.Ordinal)
            .Trim();

        if (string.IsNullOrWhiteSpace(normalizedText))
            return true;

        return decimal.TryParse(normalizedText, NumberStyles.Number | NumberStyles.AllowCurrencySymbol,
                   CultureInfo.CurrentCulture, out value) ||
               decimal.TryParse(normalizedText, NumberStyles.Number | NumberStyles.AllowCurrencySymbol,
                   CultureInfo.InvariantCulture, out value);
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

        if (IsCashLike && string.IsNullOrWhiteSpace(PrimaryAmountText))
            return false;

        if (IsCreditLike && string.IsNullOrWhiteSpace(SpentAmountText))
            return false;

        if (IsCredit && string.IsNullOrWhiteSpace(AccountLimitText))
            return false;

        if (IsSaving && string.IsNullOrWhiteSpace(ApyText))
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
            PrimaryAmountText ?? string.Empty,
            SpentAmountText ?? string.Empty,
            AccountLimitText ?? string.Empty,
            ApyText ?? string.Empty,
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

    private readonly record struct AddSpendingSourceInput(
        string Name,
        SpendingSourceType SpendingSourceType,
        decimal Balance,
        decimal SpentAmount,
        decimal AccountLimit,
        int? MonthlyDueDate,
        int? DeductSource,
        decimal? InterestRate,
        bool ShowOnUI,
        bool IsEnabled);

    private readonly record struct FormState(
        string NameText,
        string PrimaryAmountText,
        string SpentAmountText,
        string AccountLimitText,
        string ApyText,
        string MonthlyDueDateText,
        int? SelectedDeductSource,
        SpendingSourceType SpendingSourceType,
        bool ShowOnUI,
        bool IsEnabled);

    public readonly record struct DeductSourceOption(int Id, string Name);
}
