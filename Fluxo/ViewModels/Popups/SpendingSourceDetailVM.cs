using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
using Fluxo.Services.History;
using Fluxo.ViewModels.Messages;
using Fluxo.ViewModels.Shell;

namespace Fluxo.ViewModels.Popups;

public partial class SpendingSourceDetailVM : ObservableObject
{
    private readonly MainVM _mainViewModel;
    private readonly int _spendingSourceId;
    private readonly IUnitOfWork _uow;
    private SpendingSourceDetailState _savedState = SpendingSourceDetailState.Empty;

    [ObservableProperty] private string _accountLimitText = string.Empty;
    [ObservableProperty] private string _apyText = string.Empty;
    [ObservableProperty] private DateTime? _dueDate;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private decimal _moneyIn;
    [ObservableProperty] private decimal _moneyOut;
    [ObservableProperty] private string _nameText = string.Empty;
    [ObservableProperty] private string _primaryAmountText = string.Empty;
    [ObservableProperty] private bool _showOnUI = true;
    [ObservableProperty] private SpendingSourceType _spendingSourceType;
    [ObservableProperty] private string _spentAmountText = string.Empty;
    [ObservableProperty] private decimal _trendMaximum = 1m;

    public SpendingSourceDetailVM(MainVM mainViewModel, int spendingSourceId, IUnitOfWork uow)
    {
        _mainViewModel = mainViewModel;
        _spendingSourceId = spendingSourceId;
        _uow = uow;
    }

    public ObservableCollection<SpendingSourceActivityItemVM> RecentActivities { get; } = [];
    public ObservableCollection<SpendingSourceTrendItemVM> Trends { get; } = [];

    public MainVM MainViewModel => _mainViewModel;

    public IUnitOfWork UnitOfWork => _uow;

    public int SpendingSourceId => _spendingSourceId;

    public string PopupTitle => "Income Detail";

    public bool IsCashOrChecking => SpendingSourceType is SpendingSourceType.Cash or SpendingSourceType.Checking;

    public bool IsCredit => SpendingSourceType == SpendingSourceType.Credit;

    public bool IsBnpl => SpendingSourceType == SpendingSourceType.BNPL;

    public bool IsSaving => SpendingSourceType == SpendingSourceType.Saving;

    public bool CanTransfer => SpendingSourceType is not (SpendingSourceType.Credit or SpendingSourceType.BNPL) && !IsEditing;

    public bool CanDelete => !IsEditing;

    public string DisableButtonLabel => IsEditing ? "Cancel" : ShowOnUI ? "Disable" : "Enable";

    public string EditButtonLabel => IsEditing ? "Save" : "Edit";

    public decimal DisplayPrimaryAmount => SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL
        ? ParseDecimalOrDefault(SpentAmountText)
        : ParseDecimalOrDefault(PrimaryAmountText);

    public string PrimaryAmountLabel => SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL
        ? "Spent"
        : "Balance";

    partial void OnIsEditingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanTransfer));
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(DisableButtonLabel));
        OnPropertyChanged(nameof(EditButtonLabel));
    }

    partial void OnShowOnUIChanged(bool value)
    {
        OnPropertyChanged(nameof(DisableButtonLabel));
    }

    partial void OnSpendingSourceTypeChanged(SpendingSourceType value)
    {
        OnPropertyChanged(nameof(IsCashOrChecking));
        OnPropertyChanged(nameof(IsCredit));
        OnPropertyChanged(nameof(IsBnpl));
        OnPropertyChanged(nameof(IsSaving));
        OnPropertyChanged(nameof(CanTransfer));
        OnPropertyChanged(nameof(DisplayPrimaryAmount));
        OnPropertyChanged(nameof(PrimaryAmountLabel));
    }

    partial void OnPrimaryAmountTextChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayPrimaryAmount));
    }

    partial void OnSpentAmountTextChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayPrimaryAmount));
    }

    public async Task<bool> LoadAsync()
    {
        return await RefreshAsync(resetDraft: true);
    }

    public void BeginEditing()
    {
        IsEditing = true;
    }

    public void CancelEditing()
    {
        IsEditing = false;
        LoadFromState(_savedState);
    }

    public async Task<SpendingSourceDetailResult> SaveAsync()
    {
        if (!TryBuildInput(out var input, out var validationMessage))
            return SpendingSourceDetailResult.Failure(validationMessage);

        if (input == _savedState)
        {
            IsEditing = false;
            LoadFromState(_savedState);
            return SpendingSourceDetailResult.Success();
        }

        if (IsBusy)
            return SpendingSourceDetailResult.Failure("This spending source is already being updated.");

        IsBusy = true;

        try
        {
            var spendingSource = await _uow.SpendingSources.GetByIdAsync(_spendingSourceId);
            if (spendingSource is null)
                return SpendingSourceDetailResult.Failure("Unable to load this spending source.");

            var beforeSnapshot = SpendingSourceMemorySnapshot.Create(spendingSource);

            ApplyInput(spendingSource, input);

            _uow.SpendingSources.Update(spendingSource);
            await _uow.SaveChangesAsync();

            var afterSnapshot = SpendingSourceMemorySnapshot.Create(spendingSource);
            WeakReferenceMessenger.Default.Send(
                new RecordLogMemoryMessage(new EditSpendingSourceMemoryAction(beforeSnapshot, afterSnapshot)));

            await _mainViewModel.ReloadCurrentDataAsync();
            await RefreshAsync(resetDraft: true);
            IsEditing = false;

            return SpendingSourceDetailResult.Success();
        }
        catch (Exception exception)
        {
            return SpendingSourceDetailResult.Failure(
                $"Unable to save this spending source.\n\n{exception.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<SpendingSourceDetailResult> ToggleVisibilityAsync()
    {
        if (IsEditing)
        {
            CancelEditing();
            return SpendingSourceDetailResult.Success();
        }

        if (IsBusy)
            return SpendingSourceDetailResult.Failure("This spending source is already being updated.");

        IsBusy = true;

        try
        {
            var spendingSource = await _uow.SpendingSources.GetByIdAsync(_spendingSourceId);
            if (spendingSource is null)
                return SpendingSourceDetailResult.Failure("Unable to load this spending source.");

            var beforeSnapshot = SpendingSourceMemorySnapshot.Create(spendingSource);

            spendingSource.ShowOnUI = !spendingSource.ShowOnUI;
            _uow.SpendingSources.Update(spendingSource);
            await _uow.SaveChangesAsync();

            var afterSnapshot = SpendingSourceMemorySnapshot.Create(spendingSource);
            WeakReferenceMessenger.Default.Send(
                new RecordLogMemoryMessage(new EditSpendingSourceMemoryAction(beforeSnapshot, afterSnapshot)));

            await _mainViewModel.ReloadCurrentDataAsync();
            await RefreshAsync(resetDraft: true);

            return SpendingSourceDetailResult.Success();
        }
        catch (Exception exception)
        {
            return SpendingSourceDetailResult.Failure(
                $"Unable to update this spending source.\n\n{exception.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<SpendingSourceDetailResult> DeleteAsync()
    {
        if (IsBusy)
            return SpendingSourceDetailResult.Failure("This spending source is already being updated.");

        IsBusy = true;

        try
        {
            var expenseLogs = await _uow.ExpenseLogs.GetBySpendingSourceIdAsync(_spendingSourceId);
            var incomeLogs = await _uow.IncomeLogs.GetBySpendingSourceIdAsync(_spendingSourceId);

            if (expenseLogs.Any(log => !log.IsForDeletion) || incomeLogs.Count > 0)
            {
                return SpendingSourceDetailResult.Failure(
                    "This spending source still has activity, so it can't be deleted yet.");
            }

            var spendingSource = await _uow.SpendingSources.GetByIdAsync(_spendingSourceId);
            if (spendingSource is null)
                return SpendingSourceDetailResult.Failure("Unable to load this spending source.");

            var snapshot = SpendingSourceMemorySnapshot.Create(spendingSource);

            _uow.SpendingSources.Remove(spendingSource);
            await _uow.SaveChangesAsync();

            WeakReferenceMessenger.Default.Send(
                new RecordLogMemoryMessage(new DeleteSpendingSourceMemoryAction(snapshot)));

            await _mainViewModel.ReloadCurrentDataAsync();

            return SpendingSourceDetailResult.Success(shouldClose: true);
        }
        catch (Exception exception)
        {
            return SpendingSourceDetailResult.Failure(
                $"Unable to delete this spending source.\n\n{exception.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public bool HasValidChangesToPersistOnClose()
    {
        return IsEditing && TryBuildInput(out var input, out _) && input != _savedState;
    }

    private async Task<bool> RefreshAsync(bool resetDraft)
    {
        var spendingSource = await _uow.SpendingSources.GetByIdAsync(_spendingSourceId);
        if (spendingSource is null)
            return false;

        var expenseLogs = (await _uow.ExpenseLogs.GetBySpendingSourceIdAsync(_spendingSourceId))
            .Where(log => !log.IsForDeletion)
            .ToList();
        var incomeLogs = (await _uow.IncomeLogs.GetBySpendingSourceIdAsync(_spendingSourceId)).ToList();

        MoneyIn = incomeLogs.Sum(log => log.Amount);
        MoneyOut = expenseLogs.Sum(log => log.Amount);

        ReplaceCollection(RecentActivities, BuildActivities(expenseLogs, incomeLogs));
        ReplaceCollection(Trends, BuildTrends(expenseLogs, incomeLogs));
        TrendMaximum = Math.Max(1m, Trends.SelectMany(item => new[] { item.IncomeAmount, item.ExpenseAmount }).DefaultIfEmpty(1m).Max());

        _savedState = CreateState(spendingSource);

        if (resetDraft || !IsEditing)
            LoadFromState(_savedState);

        return true;
    }

    private void LoadFromState(SpendingSourceDetailState state)
    {
        SpendingSourceType = state.SpendingSourceType;
        NameText = state.Name;
        PrimaryAmountText = FormatDecimal(state.PrimaryAmount);
        SpentAmountText = FormatDecimal(state.SpentAmount);
        AccountLimitText = FormatDecimal(state.AccountLimit);
        ApyText = state.InterestRate.HasValue ? FormatDecimal(state.InterestRate.Value) : string.Empty;
        DueDate = state.DueDate;
        ShowOnUI = state.ShowOnUI;
    }

    private bool TryBuildInput(out SpendingSourceDetailState input, out string validationMessage)
    {
        input = SpendingSourceDetailState.Empty;
        validationMessage = string.Empty;

        var name = NameText.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            validationMessage = "Please enter a spending source name.";
            return false;
        }

        var primaryAmount = 0m;
        if (!TryParseDecimal(PrimaryAmountText, out primaryAmount))
        {
            validationMessage = $"{PrimaryAmountLabel} must be a valid amount.";
            return false;
        }

        var spentAmount = 0m;
        if (!TryParseDecimal(SpentAmountText, out spentAmount))
        {
            validationMessage = "Spent must be a valid amount.";
            return false;
        }

        var accountLimit = 0m;
        if (!TryParseDecimal(AccountLimitText, out accountLimit))
        {
            validationMessage = "Limit must be a valid amount.";
            return false;
        }

        decimal? interestRate = null;
        if (!string.IsNullOrWhiteSpace(ApyText))
        {
            if (!TryParseDecimal(ApyText, out var parsedInterestRate))
            {
                validationMessage = "APY must be a valid amount.";
                return false;
            }

            interestRate = parsedInterestRate;
        }

        if (primaryAmount < 0m || spentAmount < 0m || accountLimit < 0m || interestRate < 0m)
        {
            validationMessage = "Values must be zero or greater.";
            return false;
        }

        input = new SpendingSourceDetailState(
            Name: name,
            SpendingSourceType: SpendingSourceType,
            PrimaryAmount: primaryAmount,
            AccountLimit: accountLimit,
            SpentAmount: spentAmount,
            DueDate: DueDate?.Date,
            InterestRate: interestRate,
            ShowOnUI: ShowOnUI);

        return true;
    }

    private static void ApplyInput(SpendingSource spendingSource, SpendingSourceDetailState input)
    {
        spendingSource.Name = input.Name;
        spendingSource.AccountLimit = input.AccountLimit;
        spendingSource.SpentAmount = input.SpentAmount;
        spendingSource.DueDate = input.DueDate;
        spendingSource.InterestRate = input.InterestRate;
        spendingSource.ShowOnUI = input.ShowOnUI;

        if (input.SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL)
        {
            spendingSource.SpentAmount = input.SpentAmount;
        }
        else
        {
            spendingSource.Balance = input.PrimaryAmount;
        }
    }

    private static SpendingSourceDetailState CreateState(SpendingSource spendingSource)
    {
        return new SpendingSourceDetailState(
            spendingSource.Name,
            spendingSource.SpendingSourceType,
            spendingSource.SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL
                ? spendingSource.SpentAmount
                : spendingSource.Balance,
            spendingSource.AccountLimit,
            spendingSource.SpentAmount,
            spendingSource.DueDate,
            spendingSource.InterestRate,
            spendingSource.ShowOnUI);
    }

    private static IEnumerable<SpendingSourceActivityItemVM> BuildActivities(
        IEnumerable<ExpenseLog> expenseLogs,
        IEnumerable<IncomeLog> incomeLogs)
    {
        var expenseActivities = expenseLogs.Select(log => new SpendingSourceActivityItemVM(
            log.DeductedOn,
            log.Expense?.Name?.Trim() is { Length: > 0 } expenseName ? expenseName : "Expense",
            string.IsNullOrWhiteSpace(log.Notes) ? "Expense" : log.Notes.Trim(),
            log.Amount,
            IsExpense: true));

        var incomeActivities = incomeLogs.Select(log => new SpendingSourceActivityItemVM(
            log.AddedOn,
            BuildIncomeTitle(log.Notes),
            string.IsNullOrWhiteSpace(log.Notes) ? "Income" : log.Notes.Trim(),
            log.Amount,
            IsExpense: false));

        return expenseActivities
            .Concat(incomeActivities)
            .OrderByDescending(item => item.Date)
            .Take(8)
            .ToList();
    }

    private static IEnumerable<SpendingSourceTrendItemVM> BuildTrends(
        IEnumerable<ExpenseLog> expenseLogs,
        IEnumerable<IncomeLog> incomeLogs)
    {
        var months = Enumerable.Range(0, 4)
            .Select(offset => new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-3 + offset))
            .ToList();

        var incomeByMonth = incomeLogs
            .GroupBy(log => new DateTime(log.AddedOn.Year, log.AddedOn.Month, 1))
            .ToDictionary(group => group.Key, group => group.Sum(log => log.Amount));

        var expenseByMonth = expenseLogs
            .GroupBy(log => new DateTime(log.DeductedOn.Year, log.DeductedOn.Month, 1))
            .ToDictionary(group => group.Key, group => group.Sum(log => log.Amount));

        return months.Select(month => new SpendingSourceTrendItemVM(
            month,
            month.Year == DateTime.Today.Year ? month.ToString("MMM") : month.ToString("MMM yy"),
            incomeByMonth.GetValueOrDefault(month),
            expenseByMonth.GetValueOrDefault(month)))
            .ToList();
    }

    private static string BuildIncomeTitle(string notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            return "Income";

        var firstMeaningfulLine = notes
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));

        return string.IsNullOrWhiteSpace(firstMeaningfulLine) ? "Income" : firstMeaningfulLine;
    }

    private static string FormatDecimal(decimal value)
    {
        return value.ToString("0.##", CultureInfo.CurrentCulture);
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

    private static decimal ParseDecimalOrDefault(string text)
    {
        return TryParseDecimal(text, out var value) ? value : 0m;
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();

        foreach (var item in items)
            target.Add(item);
    }

    public readonly record struct SpendingSourceDetailResult(bool IsSuccess, bool ShouldClose, string? ErrorMessage)
    {
        public static SpendingSourceDetailResult Success(bool shouldClose = false) => new(true, shouldClose, null);

        public static SpendingSourceDetailResult Failure(string? errorMessage) => new(false, false, errorMessage);
    }

    private readonly record struct SpendingSourceDetailState(
        string Name,
        SpendingSourceType SpendingSourceType,
        decimal PrimaryAmount,
        decimal AccountLimit,
        decimal SpentAmount,
        DateTime? DueDate,
        decimal? InterestRate,
        bool ShowOnUI)
    {
        public static SpendingSourceDetailState Empty => new(
            string.Empty,
            SpendingSourceType.Checking,
            0m,
            0m,
            0m,
            null,
            null,
            true);
    }
}

public sealed record SpendingSourceActivityItemVM(
    DateTime Date,
    string Title,
    string Detail,
    decimal Amount,
    bool IsExpense)
{
    public string DirectionLabel => IsExpense ? "Expense" : "Income";
}

public sealed record SpendingSourceTrendItemVM(
    DateTime Month,
    string Label,
    decimal IncomeAmount,
    decimal ExpenseAmount);
