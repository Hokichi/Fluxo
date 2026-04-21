using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
using Fluxo.Resources.Messages;
using Fluxo.Services.History;
using Fluxo.ViewModels.Helpers;
using Fluxo.ViewModels.Shell;
using MainVM = Fluxo.ViewModels.Shell.Main.MainVM;

namespace Fluxo.ViewModels.Popups;

public partial class SpendingSourceDetailVM : ObservableObject
{
    [ObservableProperty] private decimal _accountLimitText;
    [ObservableProperty] private decimal _apyText;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private bool _isEnabled = true;
    [ObservableProperty] private string _monthlyDueDateText = string.Empty;
    [ObservableProperty] private decimal _moneyIn;
    [ObservableProperty] private decimal _moneyOut;
    [ObservableProperty] private string _nameText = string.Empty;
    [ObservableProperty] private decimal _primaryAmountText;
    [ObservableProperty] private int? _selectedDeductSource;
    private SpendingSourceDetailState _savedState = SpendingSourceDetailState.Empty;
    [ObservableProperty] private bool _showOnUI = true;
    [ObservableProperty] private SpendingSourceType _spendingSourceType;
    [ObservableProperty] private decimal _spentAmountText;
    [ObservableProperty] private decimal _trendMaximum = 1m;

    public SpendingSourceDetailVM(MainVM mainViewModel, int spendingSourceId, IUnitOfWork uow)
    {
        MainViewModel = mainViewModel;
        SpendingSourceId = spendingSourceId;
        UnitOfWork = uow;
    }

    public ObservableCollection<SpendingSourceActivityItemVM> RecentActivities { get; } = [];
    public ObservableCollection<SpendingSourceTrendItemVM> Trends { get; } = [];
    public ObservableCollection<DeductSourceOption> DeductSources { get; } = [];

    public MainVM MainViewModel { get; }

    public IUnitOfWork UnitOfWork { get; }

    public int SpendingSourceId { get; }

    public string PopupTitle => "Income Detail";

    public bool IsCashOrChecking => SpendingSourceType is SpendingSourceType.Cash or SpendingSourceType.Checking;

    public bool IsCredit => SpendingSourceType == SpendingSourceType.Credit;

    public bool IsBnpl => SpendingSourceType == SpendingSourceType.BNPL;

    public bool IsCreditLike => IsCredit || IsBnpl;

    public bool IsSaving => SpendingSourceType == SpendingSourceType.Saving;

    public bool CanTransfer => IsEnabled &&
                               SpendingSourceType is not (SpendingSourceType.Credit or SpendingSourceType.BNPL) &&
                               !IsEditing;

    public bool CanDelete => !IsEditing;

    public bool CanHideOrUnhide => IsEnabled && !IsEditing;

    public string EditButtonLabel => IsEditing ? "Save" : "Edit";

    public bool IsHidden => !ShowOnUI;

    public decimal DisplayPrimaryAmount => SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL
        ? SpentAmountText
        : PrimaryAmountText;

    public string PrimaryAmountLabel => SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL
        ? "Spent"
        : "Balance";

    public string MonthlyDueDateDisplay => TryParseMonthlyDueDate(MonthlyDueDateText, out var dueDay)
        ? $"Day {dueDay}"
        : "Not set";

    public string DeductSourceDisplay =>
        DeductSources.FirstOrDefault(option => option.Id == SelectedDeductSource).Name ?? "Not set";

    partial void OnIsEditingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanTransfer));
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(EditButtonLabel));
        OnPropertyChanged(nameof(CanHideOrUnhide));
    }

    partial void OnShowOnUIChanged(bool value)
    {
        OnPropertyChanged(nameof(IsHidden));
    }

    partial void OnIsEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(CanTransfer));
        OnPropertyChanged(nameof(CanHideOrUnhide));
    }

    partial void OnSpendingSourceTypeChanged(SpendingSourceType value)
    {
        OnPropertyChanged(nameof(IsCashOrChecking));
        OnPropertyChanged(nameof(IsCredit));
        OnPropertyChanged(nameof(IsBnpl));
        OnPropertyChanged(nameof(IsCreditLike));
        OnPropertyChanged(nameof(IsSaving));
        OnPropertyChanged(nameof(CanTransfer));
        OnPropertyChanged(nameof(DisplayPrimaryAmount));
        OnPropertyChanged(nameof(PrimaryAmountLabel));
        OnPropertyChanged(nameof(DeductSourceDisplay));

        if (value is not (SpendingSourceType.Credit or SpendingSourceType.BNPL))
        {
            MonthlyDueDateText = string.Empty;
            SelectedDeductSource = null;
        }
    }

    partial void OnPrimaryAmountTextChanged(decimal value)
    {
        OnPropertyChanged(nameof(DisplayPrimaryAmount));
    }

    partial void OnSpentAmountTextChanged(decimal value)
    {
        OnPropertyChanged(nameof(DisplayPrimaryAmount));
    }

    partial void OnMonthlyDueDateTextChanged(string value)
    {
        OnPropertyChanged(nameof(MonthlyDueDateDisplay));
    }

    partial void OnSelectedDeductSourceChanged(int? value)
    {
        OnPropertyChanged(nameof(DeductSourceDisplay));
    }

    public async Task<bool> LoadAsync()
    {
        return await RefreshAsync(true);
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
            var spendingSource = await UnitOfWork.SpendingSources.GetByIdAsync(SpendingSourceId);
            if (spendingSource is null)
                return SpendingSourceDetailResult.Failure("Unable to load this spending source.");

            var beforeSnapshot = SpendingSourceMemorySnapshot.Create(spendingSource);

            ApplyInput(spendingSource, input);

            UnitOfWork.SpendingSources.Update(spendingSource);
            await UnitOfWork.SaveChangesAsync();

            var afterSnapshot = SpendingSourceMemorySnapshot.Create(spendingSource);
            WeakReferenceMessenger.Default.Send(
                new RecordLogMemoryMessage(new EditSpendingSourceMemoryAction(beforeSnapshot, afterSnapshot)));
            WeakReferenceMessenger.Default.Send(new DashboardDataInvalidatedMessage(
                DashboardDataInvalidationScope.Budget | DashboardDataInvalidationScope.Notifications));

            await MainViewModel.ReloadCurrentDataAsync();
            await RefreshAsync(true);
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
            return SpendingSourceDetailResult.Failure("Finish editing before hiding or unhiding this source.");

        if (!IsEnabled)
            return SpendingSourceDetailResult.Failure("Enable this source before hiding or unhiding it.");

        if (IsBusy)
            return SpendingSourceDetailResult.Failure("This spending source is already being updated.");

        IsBusy = true;

        try
        {
            var spendingSource = await UnitOfWork.SpendingSources.GetByIdAsync(SpendingSourceId);
            if (spendingSource is null)
                return SpendingSourceDetailResult.Failure("Unable to load this spending source.");

            var beforeSnapshot = SpendingSourceMemorySnapshot.Create(spendingSource);

            spendingSource.ShowOnUI = !spendingSource.ShowOnUI;
            UnitOfWork.SpendingSources.Update(spendingSource);
            await UnitOfWork.SaveChangesAsync();

            var afterSnapshot = SpendingSourceMemorySnapshot.Create(spendingSource);
            WeakReferenceMessenger.Default.Send(
                new RecordLogMemoryMessage(new EditSpendingSourceMemoryAction(beforeSnapshot, afterSnapshot)));
            WeakReferenceMessenger.Default.Send(new DashboardDataInvalidatedMessage(
                DashboardDataInvalidationScope.Budget | DashboardDataInvalidationScope.Notifications));

            await MainViewModel.ReloadCurrentDataAsync();
            await RefreshAsync(true);

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

    public async Task<SpendingSourceDetailResult> ToggleEnabledAsync()
    {
        if (IsEditing)
            return SpendingSourceDetailResult.Failure("Finish editing before enabling or disabling this source.");

        if (IsBusy)
            return SpendingSourceDetailResult.Failure("This spending source is already being updated.");

        IsBusy = true;

        try
        {
            var spendingSource = await UnitOfWork.SpendingSources.GetByIdAsync(SpendingSourceId);
            if (spendingSource is null)
                return SpendingSourceDetailResult.Failure("Unable to load this spending source.");

            var beforeSnapshot = SpendingSourceMemorySnapshot.Create(spendingSource);

            spendingSource.IsEnabled = !spendingSource.IsEnabled;
            UnitOfWork.SpendingSources.Update(spendingSource);
            await UnitOfWork.SaveChangesAsync();

            var afterSnapshot = SpendingSourceMemorySnapshot.Create(spendingSource);
            WeakReferenceMessenger.Default.Send(
                new RecordLogMemoryMessage(new EditSpendingSourceMemoryAction(beforeSnapshot, afterSnapshot)));
            WeakReferenceMessenger.Default.Send(new DashboardDataInvalidatedMessage(
                DashboardDataInvalidationScope.Budget | DashboardDataInvalidationScope.Notifications));

            await MainViewModel.ReloadCurrentDataAsync();
            await RefreshAsync(true);

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
            var allExpenseLogs = await UnitOfWork.ExpenseLogs.GetAllAsync();
            var expenseLogs = allExpenseLogs.Where(log => log.SpendingSourceId == SpendingSourceId).ToList();
            var allIncomeLogs = await UnitOfWork.IncomeLogs.GetAllAsync();
            var incomeLogs = allIncomeLogs.Where(log => log.SpendingSourceId == SpendingSourceId).ToList();

            if (expenseLogs.Any(log => !log.IsForDeletion) || incomeLogs.Count > 0)
                return SpendingSourceDetailResult.Failure(
                    "This spending source still has activity, so it can't be deleted yet.");

            var spendingSource = await UnitOfWork.SpendingSources.GetByIdAsync(SpendingSourceId);
            if (spendingSource is null)
                return SpendingSourceDetailResult.Failure("Unable to load this spending source.");

            var snapshot = SpendingSourceMemorySnapshot.Create(spendingSource);

            UnitOfWork.SpendingSources.Remove(spendingSource);
            await UnitOfWork.SaveChangesAsync();

            WeakReferenceMessenger.Default.Send(
                new RecordLogMemoryMessage(new DeleteSpendingSourceMemoryAction(snapshot)));
            WeakReferenceMessenger.Default.Send(new DashboardDataInvalidatedMessage(
                DashboardDataInvalidationScope.Budget | DashboardDataInvalidationScope.Notifications));

            await MainViewModel.ReloadCurrentDataAsync();

            return SpendingSourceDetailResult.Success(true);
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
        var spendingSource = await UnitOfWork.SpendingSources.GetByIdAsync(SpendingSourceId);
        if (spendingSource is null)
            return false;

        await LoadDeductSourcesAsync();

        var allExpenseLogs = await UnitOfWork.ExpenseLogs.GetAllAsync();
        var expenseLogs = allExpenseLogs
            .Where(log => log.SpendingSourceId == SpendingSourceId && !log.IsForDeletion)
            .ToList();
        var allIncomeLogsRaw = await UnitOfWork.IncomeLogs.GetAllAsync();
        var incomeLogs = allIncomeLogsRaw.Where(log => log.SpendingSourceId == SpendingSourceId).ToList();

        MoneyIn = incomeLogs.Sum(log => log.Amount);
        MoneyOut = expenseLogs.Sum(log => log.Amount);

        ReplaceCollection(RecentActivities, BuildActivities(expenseLogs, incomeLogs));
        ReplaceCollection(Trends, BuildTrends(expenseLogs, incomeLogs));
        TrendMaximum = Math.Max(1m,
            Trends.SelectMany(item => new[] { item.IncomeAmount, item.ExpenseAmount }).DefaultIfEmpty(1m).Max());

        _savedState = CreateState(spendingSource);

        if (resetDraft || !IsEditing)
            LoadFromState(_savedState);

        return true;
    }

    private void LoadFromState(SpendingSourceDetailState state)
    {
        SpendingSourceType = state.SpendingSourceType;
        NameText = state.Name;
        PrimaryAmountText = state.PrimaryAmount;
        SpentAmountText = state.SpentAmount;
        AccountLimitText = state.AccountLimit;
        ApyText = state.InterestRate ?? 0m;
        MonthlyDueDateText = state.MonthlyDueDate?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        SelectedDeductSource = state.DeductSource;
        IsEnabled = state.IsEnabled;
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

        var primaryAmount = PrimaryAmountText;
        var spentAmount = SpentAmountText;
        var accountLimit = AccountLimitText;
        decimal? interestRate = IsSaving ? ApyText : null;

        if (primaryAmount < 0m || spentAmount < 0m || accountLimit < 0m ||
            (interestRate.HasValue && interestRate.Value < 0m))
        {
            validationMessage = "Values must be zero or greater.";
            return false;
        }

        int? monthlyDueDate = null;
        int? deductSource = null;
        if (SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL)
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

            deductSource = SelectedDeductSource;
        }

        input = new SpendingSourceDetailState(
            name,
            SpendingSourceType,
            primaryAmount,
            accountLimit,
            spentAmount,
            monthlyDueDate,
            deductSource,
            interestRate,
            IsEnabled,
            ShowOnUI);

        return true;
    }

    private static void ApplyInput(SpendingSource spendingSource, SpendingSourceDetailState input)
    {
        spendingSource.Name = input.Name;
        spendingSource.AccountLimit = input.AccountLimit;
        spendingSource.SpentAmount = input.SpentAmount;
        spendingSource.MonthlyDueDate = input.MonthlyDueDate;
        spendingSource.DeductSource = input.DeductSource;
        spendingSource.InterestRate = input.InterestRate;
        spendingSource.IsEnabled = input.IsEnabled;
        spendingSource.ShowOnUI = input.ShowOnUI;

        if (input.SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL)
        {
            spendingSource.SpentAmount = input.SpentAmount;
            return;
        }

        spendingSource.MonthlyDueDate = null;
        spendingSource.DeductSource = null;
        spendingSource.AccountLimit = 0m;
        spendingSource.Balance = input.PrimaryAmount;
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
            MonthlyDueDateHelper.Normalize(spendingSource.MonthlyDueDate),
            spendingSource.DeductSource,
            spendingSource.InterestRate,
            spendingSource.IsEnabled,
            spendingSource.ShowOnUI);
    }

    private async Task LoadDeductSourcesAsync()
    {
        var options = (await UnitOfWork.SpendingSources.GetAllAsync())
            .Where(source => source.Id != SpendingSourceId)
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

    private static IEnumerable<SpendingSourceActivityItemVM> BuildActivities(
        IEnumerable<ExpenseLog> expenseLogs,
        IEnumerable<IncomeLog> incomeLogs)
    {
        var expenseActivities = expenseLogs.Select(log => new SpendingSourceActivityItemVM(
            log.DeductedOn,
            log.Expense?.Name?.Trim() is { Length: > 0 } expenseName ? expenseName : "Expense",
            string.IsNullOrWhiteSpace(log.Notes) ? "Expense" : log.Notes.Trim(),
            log.Amount,
            true));

        var incomeActivities = incomeLogs.Select(log => new SpendingSourceActivityItemVM(
            log.AddedOn,
            BuildIncomeTitle(log.Notes),
            string.IsNullOrWhiteSpace(log.Notes) ? "Income" : log.Notes.Trim(),
            log.Amount,
            false));

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

    private static bool TryParseMonthlyDueDate(string text, out int monthlyDueDate)
    {
        monthlyDueDate = 0;
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out monthlyDueDate) &&
               monthlyDueDate is >= MonthlyDueDateHelper.MinMonthlyDay and <= MonthlyDueDateHelper.MaxMonthlyDay;
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();

        foreach (var item in items)
            target.Add(item);
    }

    public readonly record struct SpendingSourceDetailResult(bool IsSuccess, bool ShouldClose, string? ErrorMessage)
    {
        public static SpendingSourceDetailResult Success(bool shouldClose = false)
        {
            return new SpendingSourceDetailResult(true, shouldClose, null);
        }

        public static SpendingSourceDetailResult Failure(string? errorMessage)
        {
            return new SpendingSourceDetailResult(false, false, errorMessage);
        }
    }

    private readonly record struct SpendingSourceDetailState(
        string Name,
        SpendingSourceType SpendingSourceType,
        decimal PrimaryAmount,
        decimal AccountLimit,
        decimal SpentAmount,
        int? MonthlyDueDate,
        int? DeductSource,
        decimal? InterestRate,
        bool IsEnabled,
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
            null,
            true,
            true);
    }

    public readonly record struct DeductSourceOption(int Id, string Name);
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
