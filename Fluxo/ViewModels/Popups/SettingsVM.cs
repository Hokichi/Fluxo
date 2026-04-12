using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Constants;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
using Fluxo.Services.History;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Messages;
using Fluxo.ViewModels.Shell;

namespace Fluxo.ViewModels.Popups;

public partial class SettingsVM : ObservableObject
{
    private readonly MainVM _mainViewModel;
    private readonly Func<IUnitOfWork> _unitOfWorkFactory;
    private BudgetAllocationSnapshot _savedBudgetAllocation = new(50, 30, 20);
    private readonly Dictionary<string, bool> _savedNotificationSettings = new(StringComparer.Ordinal);

    [ObservableProperty] private string _budgetAllocationErrorMessage = string.Empty;
    [ObservableProperty] private bool _isFixedExpenseChecksEnabled;
    [ObservableProperty] private bool _isGoalChecksEnabled;
    [ObservableProperty] private bool _isSpendingSourceChecksEnabled;
    [ObservableProperty] private int _investAllocationPercentage;
    [ObservableProperty] private int _needsAllocationPercentage;
    [ObservableProperty] private int _wantsAllocationPercentage;

    public SettingsVM(MainVM mainViewModel, Func<IUnitOfWork> unitOfWorkFactory)
    {
        _mainViewModel = mainViewModel;
        _unitOfWorkFactory = unitOfWorkFactory;
    }

    public ObservableCollection<SettingsSpendingSourceItemVM> SpendingSources { get; } = [];
    public ObservableCollection<SettingsFixedExpenseItemVM> FixedExpenses { get; } = [];
    public ObservableCollection<SettingsSavingGoalItemVM> SavingGoals { get; } = [];
    public ObservableCollection<ExpenseTagVM> Tags { get; } = [];
    public ObservableCollection<SettingsNotificationOptionVM> NotificationSettings { get; } = [];

    public decimal TotalBudgetAmount => _mainViewModel.TotalIncomeAmount;

    public bool HasBudgetAllocationError => !string.IsNullOrWhiteSpace(BudgetAllocationErrorMessage);

    public bool HasPendingConfigurationChanges =>
        NeedsAllocationPercentage != _savedBudgetAllocation.Needs ||
        WantsAllocationPercentage != _savedBudgetAllocation.Wants ||
        InvestAllocationPercentage != _savedBudgetAllocation.Invest ||
        NotificationSettings.Any(setting =>
            _savedNotificationSettings.TryGetValue(setting.SettingName, out var savedValue)
                ? savedValue != setting.IsEnabled
                : setting.IsEnabled);

    public string NeedsAllocationAmountText => BuildAllocationAmountText(NeedsAllocationPercentage);
    public string WantsAllocationAmountText => BuildAllocationAmountText(WantsAllocationPercentage);
    public string InvestAllocationAmountText => BuildAllocationAmountText(InvestAllocationPercentage);

    partial void OnNeedsAllocationPercentageChanged(int value) => OnAllocationChanged();
    partial void OnWantsAllocationPercentageChanged(int value) => OnAllocationChanged();
    partial void OnInvestAllocationPercentageChanged(int value) => OnAllocationChanged();

    public async Task LoadAsync()
    {
        await using var unitOfWork = _unitOfWorkFactory();

        var settings = await unitOfWork.UserSettings.GetAllAsync();
        var settingsByName = settings.ToDictionary(setting => setting.Name, setting => setting.Value, StringComparer.Ordinal);
        var hiddenFixedExpenseIds = ParseIdSet(settingsByName, UserSettingNames.HiddenFixedExpenseIds);
        var hiddenSavingGoalIds = ParseIdSet(settingsByName, UserSettingNames.HiddenSavingGoalIds);
        var disabledSavingGoalIds = ParseIdSet(settingsByName, UserSettingNames.DisabledSavingGoalIds);

        NeedsAllocationPercentage = ParsePercentage(settingsByName, UserSettingNames.NeedsThreshold, 50m);
        WantsAllocationPercentage = ParsePercentage(settingsByName, UserSettingNames.WantsThreshold, 30m);
        InvestAllocationPercentage = ParsePercentage(settingsByName, UserSettingNames.InvestThreshold, 20m);
        _savedBudgetAllocation = new BudgetAllocationSnapshot(
            NeedsAllocationPercentage,
            WantsAllocationPercentage,
            InvestAllocationPercentage);

        LoadNotificationSettings(settingsByName);

        ReplaceCollection(SpendingSources, (await unitOfWork.SpendingSources.GetAllAsync())
            .OrderByDescending(source => source.ShowOnUI)
            .ThenBy(source => source.Name)
            .Select(source => new SettingsSpendingSourceItemVM(source)));

        ReplaceCollection(FixedExpenses, (await unitOfWork.Expenses.GetByKindAsync(ExpenseKind.Fixed))
            .OrderBy(expense => expense.Name)
            .Select(expense => new SettingsFixedExpenseItemVM(expense, hiddenFixedExpenseIds.Contains(expense.Id))));

        ReplaceCollection(SavingGoals, (await unitOfWork.SavingGoals.GetAllAsync())
            .OrderBy(goal => goal.SavingEndDate)
            .ThenBy(goal => goal.Name)
            .Select(goal => new SettingsSavingGoalItemVM(
                goal,
                hiddenSavingGoalIds.Contains(goal.Id),
                !disabledSavingGoalIds.Contains(goal.Id))));

        ReplaceCollection(Tags, (await unitOfWork.ExpenseTags.GetTagsByCountDescendingAsync())
            .Select(item => new ExpenseTagVM
            {
                Id = item.Tag.Id,
                Name = item.Tag.Name,
                HexCode = item.Tag.HexCode
            }));

        IsSpendingSourceChecksEnabled = false;
        IsFixedExpenseChecksEnabled = false;
        IsGoalChecksEnabled = false;
        ValidateBudgetAllocation();
        OnPropertyChanged(nameof(TotalBudgetAmount));
        OnPropertyChanged(nameof(NeedsAllocationAmountText));
        OnPropertyChanged(nameof(WantsAllocationAmountText));
        OnPropertyChanged(nameof(InvestAllocationAmountText));
        OnPropertyChanged(nameof(HasPendingConfigurationChanges));
    }

    public void IncrementAllocation(BudgetAllocationSegment segment, int delta)
    {
        switch (segment)
        {
            case BudgetAllocationSegment.Needs:
                NeedsAllocationPercentage = Math.Clamp(NeedsAllocationPercentage + delta, 0, 100);
                break;
            case BudgetAllocationSegment.Wants:
                WantsAllocationPercentage = Math.Clamp(WantsAllocationPercentage + delta, 0, 100);
                break;
            case BudgetAllocationSegment.Invest:
                InvestAllocationPercentage = Math.Clamp(InvestAllocationPercentage + delta, 0, 100);
                break;
        }
    }

    public void SetAllocation(BudgetAllocationSegment segment, double value)
    {
        var roundedValue = Math.Clamp((int)Math.Round(value, MidpointRounding.AwayFromZero), 0, 100);

        switch (segment)
        {
            case BudgetAllocationSegment.Needs:
                NeedsAllocationPercentage = roundedValue;
                break;
            case BudgetAllocationSegment.Wants:
                WantsAllocationPercentage = roundedValue;
                break;
            case BudgetAllocationSegment.Invest:
                InvestAllocationPercentage = roundedValue;
                break;
        }
    }

    public async Task<SettingsOperationResult> ApplyConfigurationAsync()
    {
        ValidateBudgetAllocation();
        if (HasBudgetAllocationError)
            return SettingsOperationResult.Failure(BudgetAllocationErrorMessage);

        await using var unitOfWork = _unitOfWorkFactory();
        var actions = new List<ILogMemoryAction>();

        await UpdateUserSettingAsync(unitOfWork, UserSettingNames.NeedsThreshold,
            NeedsAllocationPercentage.ToString(CultureInfo.InvariantCulture), actions);
        await UpdateUserSettingAsync(unitOfWork, UserSettingNames.WantsThreshold,
            WantsAllocationPercentage.ToString(CultureInfo.InvariantCulture), actions);
        await UpdateUserSettingAsync(unitOfWork, UserSettingNames.InvestThreshold,
            InvestAllocationPercentage.ToString(CultureInfo.InvariantCulture), actions);

        foreach (var notificationSetting in NotificationSettings)
            await UpdateUserSettingAsync(unitOfWork, notificationSetting.SettingName,
                notificationSetting.IsEnabled.ToString(CultureInfo.InvariantCulture), actions);

        if (actions.Count == 0)
            return SettingsOperationResult.Success();

        await unitOfWork.SaveChangesAsync();
        RecordActions(actions);
        await _mainViewModel.ReloadCurrentDataAsync();
        await LoadAsync();

        return SettingsOperationResult.Success();
    }

    public void RevertConfigurationChanges()
    {
        NeedsAllocationPercentage = _savedBudgetAllocation.Needs;
        WantsAllocationPercentage = _savedBudgetAllocation.Wants;
        InvestAllocationPercentage = _savedBudgetAllocation.Invest;

        foreach (var notificationSetting in NotificationSettings)
            if (_savedNotificationSettings.TryGetValue(notificationSetting.SettingName, out var savedValue))
                notificationSetting.IsEnabled = savedValue;

        ValidateBudgetAllocation();
        OnPropertyChanged(nameof(HasPendingConfigurationChanges));
    }

    public void ClearSelections(SettingsBatchTarget target)
    {
        foreach (var item in GetSelectableItems(target))
            item.IsChecked = false;
    }

    public async Task<SettingsOperationResult> ExecuteSpendingSourceActionAsync(SettingsBatchAction action)
    {
        var selectedIds = SpendingSources.Where(item => item.IsChecked).Select(item => item.Id).ToArray();
        if (selectedIds.Length == 0)
            return SettingsOperationResult.Failure("Select at least one spending source first.");

        await using var unitOfWork = _unitOfWorkFactory();
        var actions = new List<ILogMemoryAction>();

        try
        {
            switch (action)
            {
                case SettingsBatchAction.Delete:
                    foreach (var selectedId in selectedIds)
                    {
                        var spendingSource = await unitOfWork.SpendingSources.GetByIdAsync(selectedId);
                        if (spendingSource is null)
                            continue;

                        var expenseLogs = await unitOfWork.ExpenseLogs.GetBySpendingSourceIdAsync(selectedId);
                        var incomeLogs = await unitOfWork.IncomeLogs.GetBySpendingSourceIdAsync(selectedId);

                        if (expenseLogs.Any(log => !log.IsForDeletion) || incomeLogs.Count > 0)
                            return SettingsOperationResult.Failure(
                                $"{spendingSource.Name} still has activity, so it can't be deleted yet.");

                        var snapshot = SpendingSourceMemorySnapshot.Create(spendingSource);
                        unitOfWork.SpendingSources.Remove(spendingSource);
                        actions.Add(new DeleteSpendingSourceMemoryAction(snapshot));
                    }

                    break;
                case SettingsBatchAction.Hide:
                case SettingsBatchAction.Unhide:
                case SettingsBatchAction.Disable:
                case SettingsBatchAction.Enable:
                    foreach (var selectedId in selectedIds)
                    {
                        var spendingSource = await unitOfWork.SpendingSources.GetByIdAsync(selectedId);
                        if (spendingSource is null)
                            continue;

                        var beforeSnapshot = SpendingSourceMemorySnapshot.Create(spendingSource);
                        var updated = false;

                        switch (action)
                        {
                            case SettingsBatchAction.Hide when spendingSource.ShowOnUI:
                                spendingSource.ShowOnUI = false;
                                updated = true;
                                break;
                            case SettingsBatchAction.Unhide when !spendingSource.ShowOnUI:
                                spendingSource.ShowOnUI = true;
                                updated = true;
                                break;
                            case SettingsBatchAction.Disable when spendingSource.IsEnabled:
                                spendingSource.IsEnabled = false;
                                updated = true;
                                break;
                            case SettingsBatchAction.Enable when !spendingSource.IsEnabled:
                                spendingSource.IsEnabled = true;
                                updated = true;
                                break;
                        }

                        if (!updated)
                            continue;

                        unitOfWork.SpendingSources.Update(spendingSource);
                        var afterSnapshot = SpendingSourceMemorySnapshot.Create(spendingSource);
                        actions.Add(new EditSpendingSourceMemoryAction(beforeSnapshot, afterSnapshot));
                    }

                    break;
            }

            if (actions.Count == 0)
                return SettingsOperationResult.Failure("Nothing changed for the selected spending sources.");

            await unitOfWork.SaveChangesAsync();
            RecordActions(actions);
            await _mainViewModel.ReloadCurrentDataAsync();
            await LoadAsync();

            return SettingsOperationResult.Success();
        }
        catch (Exception exception)
        {
            return SettingsOperationResult.Failure(
                $"Unable to update the selected spending sources.\n\n{exception.Message}");
        }
    }

    public async Task<SettingsOperationResult> ExecuteFixedExpenseActionAsync(SettingsBatchAction action)
    {
        var selectedItems = FixedExpenses.Where(item => item.IsChecked).ToArray();
        if (selectedItems.Length == 0)
            return SettingsOperationResult.Failure("Select at least one fixed expense first.");

        await using var unitOfWork = _unitOfWorkFactory();
        var actions = new List<ILogMemoryAction>();

        try
        {
            switch (action)
            {
                case SettingsBatchAction.Delete:
                    var expenseLogs = await unitOfWork.ExpenseLogs.GetAllAsync();

                    foreach (var selectedItem in selectedItems)
                    {
                        if (expenseLogs.Any(log => log.ExpenseId == selectedItem.Id && !log.IsForDeletion))
                            return SettingsOperationResult.Failure(
                                $"{selectedItem.Name} still has logged activity, so it can't be deleted yet.");

                        var expense = await unitOfWork.Expenses.GetByIdAsync(selectedItem.Id);
                        if (expense is null)
                            continue;

                        var snapshot = ExpenseMemorySnapshot.Create(expense);
                        unitOfWork.Expenses.Remove(expense);
                        actions.Add(new DeleteExpenseMemoryAction(snapshot));
                    }

                    break;
                case SettingsBatchAction.Disable:
                case SettingsBatchAction.Enable:
                    foreach (var selectedItem in selectedItems)
                    {
                        var expense = await unitOfWork.Expenses.GetByIdAsync(selectedItem.Id);
                        if (expense is null)
                            continue;

                        var beforeSnapshot = ExpenseMemorySnapshot.Create(expense);
                        var updated = false;

                        if (action == SettingsBatchAction.Disable && expense.IsActive)
                        {
                            expense.IsActive = false;
                            updated = true;
                        }
                        else if (action == SettingsBatchAction.Enable && !expense.IsActive)
                        {
                            expense.IsActive = true;
                            updated = true;
                        }

                        if (!updated)
                            continue;

                        unitOfWork.Expenses.Update(expense);
                        var afterSnapshot = ExpenseMemorySnapshot.Create(expense);
                        actions.Add(new EditExpenseMemoryAction(beforeSnapshot, afterSnapshot));
                    }

                    break;
                case SettingsBatchAction.Hide:
                case SettingsBatchAction.Unhide:
                    var hiddenFixedExpenseIds = ParseIdSet(await GetSettingsDictionaryAsync(unitOfWork),
                        UserSettingNames.HiddenFixedExpenseIds);

                    if (action == SettingsBatchAction.Hide)
                        hiddenFixedExpenseIds.UnionWith(selectedItems.Select(item => item.Id));
                    else
                        hiddenFixedExpenseIds.ExceptWith(selectedItems.Select(item => item.Id));

                    await UpdateIdSetSettingAsync(unitOfWork, UserSettingNames.HiddenFixedExpenseIds,
                        hiddenFixedExpenseIds, actions);
                    break;
            }

            if (actions.Count == 0)
                return SettingsOperationResult.Failure("Nothing changed for the selected fixed expenses.");

            await unitOfWork.SaveChangesAsync();
            RecordActions(actions);
            await _mainViewModel.ReloadCurrentDataAsync();
            await LoadAsync();

            return SettingsOperationResult.Success();
        }
        catch (Exception exception)
        {
            return SettingsOperationResult.Failure(
                $"Unable to update the selected fixed expenses.\n\n{exception.Message}");
        }
    }

    public async Task<SettingsOperationResult> ExecuteGoalActionAsync(SettingsBatchAction action)
    {
        var selectedItems = SavingGoals.Where(item => item.IsChecked).ToArray();
        if (selectedItems.Length == 0)
            return SettingsOperationResult.Failure("Select at least one goal first.");

        await using var unitOfWork = _unitOfWorkFactory();
        var actions = new List<ILogMemoryAction>();

        try
        {
            switch (action)
            {
                case SettingsBatchAction.Delete:
                    foreach (var selectedItem in selectedItems)
                    {
                        var savingGoal = await unitOfWork.SavingGoals.GetByIdAsync(selectedItem.Id);
                        if (savingGoal is null)
                            continue;

                        var snapshot = SavingGoalMemorySnapshot.Create(savingGoal);
                        unitOfWork.SavingGoals.Remove(savingGoal);
                        actions.Add(new DeleteSavingGoalMemoryAction(snapshot));
                    }

                    break;
                case SettingsBatchAction.Hide:
                case SettingsBatchAction.Unhide:
                    var hiddenGoalIds = ParseIdSet(await GetSettingsDictionaryAsync(unitOfWork),
                        UserSettingNames.HiddenSavingGoalIds);

                    if (action == SettingsBatchAction.Hide)
                        hiddenGoalIds.UnionWith(selectedItems.Select(item => item.Id));
                    else
                        hiddenGoalIds.ExceptWith(selectedItems.Select(item => item.Id));

                    await UpdateIdSetSettingAsync(unitOfWork, UserSettingNames.HiddenSavingGoalIds, hiddenGoalIds,
                        actions);
                    break;
                case SettingsBatchAction.Disable:
                case SettingsBatchAction.Enable:
                    var disabledGoalIds = ParseIdSet(await GetSettingsDictionaryAsync(unitOfWork),
                        UserSettingNames.DisabledSavingGoalIds);

                    if (action == SettingsBatchAction.Disable)
                        disabledGoalIds.UnionWith(selectedItems.Select(item => item.Id));
                    else
                        disabledGoalIds.ExceptWith(selectedItems.Select(item => item.Id));

                    await UpdateIdSetSettingAsync(unitOfWork, UserSettingNames.DisabledSavingGoalIds, disabledGoalIds,
                        actions);
                    break;
            }

            if (actions.Count == 0)
                return SettingsOperationResult.Failure("Nothing changed for the selected goals.");

            await unitOfWork.SaveChangesAsync();
            RecordActions(actions);
            await _mainViewModel.ReloadCurrentDataAsync();
            await LoadAsync();

            return SettingsOperationResult.Success();
        }
        catch (Exception exception)
        {
            return SettingsOperationResult.Failure(
                $"Unable to update the selected goals.\n\n{exception.Message}");
        }
    }

    public async Task<SettingsOperationResult> DeleteTagAsync(ExpenseTagVM tag)
    {
        ArgumentNullException.ThrowIfNull(tag);

        await using var unitOfWork = _unitOfWorkFactory();

        try
        {
            var expenseTag = await unitOfWork.ExpenseTags.GetByIdAsync(tag.Id);
            if (expenseTag is null)
                return SettingsOperationResult.Failure("That tag could not be found anymore.");

            var linkedExpenses = await unitOfWork.Expenses.GetByTagIdAsync(tag.Id);
            if (linkedExpenses.Count > 0)
                return SettingsOperationResult.Failure(
                    $"{tag.Name} is still assigned to one or more expenses, so it can't be deleted yet.");

            var snapshot = ExpenseTagMemorySnapshot.Create(expenseTag);
            unitOfWork.ExpenseTags.Remove(expenseTag);
            await unitOfWork.SaveChangesAsync();

            RecordActions([new DeleteExpenseTagMemoryAction(snapshot)]);
            await _mainViewModel.ReloadCurrentDataAsync();
            await LoadAsync();

            return SettingsOperationResult.Success();
        }
        catch (Exception exception)
        {
            return SettingsOperationResult.Failure(
                $"Unable to delete this tag.\n\n{exception.Message}");
        }
    }

    private void LoadNotificationSettings(IReadOnlyDictionary<string, string> settingsByName)
    {
        foreach (var notificationSetting in NotificationSettings)
            notificationSetting.PropertyChanged -= OnNotificationSettingPropertyChanged;

        ReplaceCollection(NotificationSettings,
        [
            new SettingsNotificationOptionVM(
                "Upcoming fixed expense reminders",
                "Warn before recurring fixed expenses are due.",
                UserSettingNames.IsFixedExpensesDeductionNotifEnabled,
                ParseBool(settingsByName, UserSettingNames.IsFixedExpensesDeductionNotifEnabled, false)),
            new SettingsNotificationOptionVM(
                "Credit deadline reminders",
                "Warn when credit and BNPL due dates are approaching.",
                UserSettingNames.IsCreditDeadlineNotifEnabled,
                ParseBool(settingsByName, UserSettingNames.IsCreditDeadlineNotifEnabled, true)),
            new SettingsNotificationOptionVM(
                "Budget threshold alerts",
                "Warn when Needs, Wants, or Invest allocations are nearly spent.",
                UserSettingNames.IsBudgetThresholdNotifEnabled,
                ParseBool(settingsByName, UserSettingNames.IsBudgetThresholdNotifEnabled, true)),
            new SettingsNotificationOptionVM(
                "Low credit usage alerts",
                "Warn when credit or BNPL sources cross their usage threshold.",
                UserSettingNames.IsLowCreditNotifEnabled,
                ParseBool(settingsByName, UserSettingNames.IsLowCreditNotifEnabled, false)),
            new SettingsNotificationOptionVM(
                "Low account balance alerts",
                "Warn when checking or cash sources are running low.",
                UserSettingNames.IsLowAccountBalanceNotifEnabled,
                ParseBool(settingsByName, UserSettingNames.IsLowAccountBalanceNotifEnabled,
                    ParseBool(settingsByName, UserSettingNames.IsLowCreditNotifEnabled, false)))
        ]);

        _savedNotificationSettings.Clear();

        foreach (var notificationSetting in NotificationSettings)
        {
            notificationSetting.PropertyChanged += OnNotificationSettingPropertyChanged;
            _savedNotificationSettings[notificationSetting.SettingName] = notificationSetting.IsEnabled;
        }
    }

    private void OnNotificationSettingPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsNotificationOptionVM.IsEnabled))
            OnPropertyChanged(nameof(HasPendingConfigurationChanges));
    }

    private string BuildAllocationAmountText(int percentage)
    {
        var allocatedAmount = decimal.Round(TotalBudgetAmount * percentage / 100m, 2);
        return $"{percentage}% of ${TotalBudgetAmount.ToString("N2", CultureInfo.InvariantCulture)} = ${allocatedAmount.ToString("N2", CultureInfo.InvariantCulture)}";
    }

    private void OnAllocationChanged()
    {
        ValidateBudgetAllocation();
        OnPropertyChanged(nameof(NeedsAllocationAmountText));
        OnPropertyChanged(nameof(WantsAllocationAmountText));
        OnPropertyChanged(nameof(InvestAllocationAmountText));
        OnPropertyChanged(nameof(HasPendingConfigurationChanges));
    }

    private void ValidateBudgetAllocation()
    {
        var total = NeedsAllocationPercentage + WantsAllocationPercentage + InvestAllocationPercentage;
        BudgetAllocationErrorMessage = total == 100
            ? string.Empty
            : $"Needs, Wants, and Invest must add up to 100%. Current total: {total}%";
        OnPropertyChanged(nameof(HasBudgetAllocationError));
    }

    private async Task UpdateUserSettingAsync(IUnitOfWork unitOfWork, string name, string? value,
        List<ILogMemoryAction> actions)
    {
        var existingSetting = await unitOfWork.UserSettings.GetByNameAsync(name);
        var beforeSnapshot = existingSetting is null
            ? UserSettingMemorySnapshot.Missing(name)
            : UserSettingMemorySnapshot.Create(existingSetting);

        if (value is null)
        {
            if (existingSetting is null)
                return;

            unitOfWork.UserSettings.Remove(existingSetting);
            actions.Add(new SetUserSettingMemoryAction(beforeSnapshot, UserSettingMemorySnapshot.Missing(name)));
            return;
        }

        if (existingSetting is null)
        {
            await unitOfWork.UserSettings.AddAsync(new UserSettings { Name = name, Value = value });
        }
        else
        {
            if (string.Equals(existingSetting.Value, value, StringComparison.Ordinal))
                return;

            existingSetting.Value = value;
            unitOfWork.UserSettings.Update(existingSetting);
        }

        actions.Add(new SetUserSettingMemoryAction(beforeSnapshot, new UserSettingMemorySnapshot(name, value, true)));
    }

    private async Task UpdateIdSetSettingAsync(IUnitOfWork unitOfWork, string name, HashSet<int> ids,
        List<ILogMemoryAction> actions)
    {
        var value = ids.Count == 0
            ? null
            : string.Join(",", ids.OrderBy(id => id).Select(id => id.ToString(CultureInfo.InvariantCulture)));

        await UpdateUserSettingAsync(unitOfWork, name, value, actions);
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();
        foreach (var item in items)
            collection.Add(item);
    }

    private static int ParsePercentage(IReadOnlyDictionary<string, string> settings, string name, decimal defaultValue)
    {
        if (!settings.TryGetValue(name, out var value) ||
            !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedValue))
            return (int)defaultValue;

        return (int)Math.Round(parsedValue, MidpointRounding.AwayFromZero);
    }

    private static bool ParseBool(IReadOnlyDictionary<string, string> settings, string name, bool defaultValue)
    {
        return settings.TryGetValue(name, out var value) && bool.TryParse(value, out var parsedValue)
            ? parsedValue
            : defaultValue;
    }

    private static HashSet<int> ParseIdSet(IReadOnlyDictionary<string, string> settings, string name)
    {
        if (!settings.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
            return [];

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : -1)
            .Where(id => id > 0)
            .ToHashSet();
    }

    private static void RecordActions(IReadOnlyList<ILogMemoryAction> actions)
    {
        if (actions.Count == 0)
            return;

        WeakReferenceMessenger.Default.Send(new RecordLogMemoryMessage(
            actions.Count == 1 ? actions[0] : new CompositeLogMemoryAction("Settings update", actions)));
    }

    private static async Task<Dictionary<string, string>> GetSettingsDictionaryAsync(IUnitOfWork unitOfWork)
    {
        var settings = await unitOfWork.UserSettings.GetAllAsync();
        return settings.ToDictionary(setting => setting.Name, setting => setting.Value, StringComparer.Ordinal);
    }

    private IEnumerable<ISettingsSelectableItem> GetSelectableItems(SettingsBatchTarget target)
    {
        return target switch
        {
            SettingsBatchTarget.SpendingSources => SpendingSources,
            SettingsBatchTarget.FixedExpenses => FixedExpenses,
            SettingsBatchTarget.Goals => SavingGoals,
            _ => []
        };
    }
}

public enum BudgetAllocationSegment
{
    Needs = 1,
    Wants = 2,
    Invest = 3
}

public enum SettingsBatchAction
{
    Delete = 1,
    Hide = 2,
    Unhide = 3,
    Disable = 4,
    Enable = 5
}

public enum SettingsBatchTarget
{
    SpendingSources = 1,
    FixedExpenses = 2,
    Goals = 3
}

public interface ISettingsSelectableItem
{
    bool IsChecked { get; set; }
}

public readonly record struct BudgetAllocationSnapshot(int Needs, int Wants, int Invest);

public readonly record struct SettingsOperationResult(bool IsSuccess, string? ErrorMessage)
{
    public static SettingsOperationResult Success() => new(true, null);
    public static SettingsOperationResult Failure(string? errorMessage) => new(false, errorMessage);
}

public partial class SettingsNotificationOptionVM : ObservableObject
{
    [ObservableProperty] private bool _isEnabled;

    public SettingsNotificationOptionVM(string title, string description, string settingName, bool isEnabled)
    {
        Title = title;
        Description = description;
        SettingName = settingName;
        _isEnabled = isEnabled;
    }

    public string Title { get; }
    public string Description { get; }
    public string SettingName { get; }
}

public partial class SettingsSpendingSourceItemVM : ObservableObject, ISettingsSelectableItem
{
    [ObservableProperty] private bool _isChecked;

    public SettingsSpendingSourceItemVM(SpendingSource spendingSource)
    {
        Id = spendingSource.Id;
        Name = spendingSource.Name;
        TypeDisplayName = spendingSource.SpendingSourceType switch
        {
            SpendingSourceType.Credit => "Credit",
            SpendingSourceType.BNPL => "BNPL",
            SpendingSourceType.Checking => "Checking",
            SpendingSourceType.Cash => "Cash",
            SpendingSourceType.Saving => "Savings",
            _ => "Source"
        };
        PrimaryAmount = spendingSource.SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL
            ? spendingSource.SpentAmount
            : spendingSource.Balance;
        PrimaryAmountLabel = spendingSource.SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL
            ? "Spent"
            : "Balance";
        IsEnabled = spendingSource.IsEnabled;
        IsHidden = !spendingSource.ShowOnUI;
    }

    public int Id { get; }
    public string Name { get; }
    public string TypeDisplayName { get; }
    public decimal PrimaryAmount { get; }
    public string PrimaryAmountLabel { get; }
    public bool IsEnabled { get; }
    public bool IsHidden { get; }
}

public partial class SettingsFixedExpenseItemVM : ObservableObject, ISettingsSelectableItem
{
    [ObservableProperty] private bool _isChecked;

    public SettingsFixedExpenseItemVM(Expense expense, bool isHidden)
    {
        Id = expense.Id;
        Name = expense.Name;
        Amount = expense.Amount;
        TagName = expense.ExpenseTag?.Name ?? "Untagged";
        SpendingSourceName = expense.SpendingSource?.Name ?? "No source";
        RecurringDate = expense.RecurringDate;
        IsEnabled = expense.IsActive;
        IsHidden = isHidden;
    }

    public int Id { get; }
    public string Name { get; }
    public decimal Amount { get; }
    public string TagName { get; }
    public string SpendingSourceName { get; }
    public DateTime? RecurringDate { get; }
    public bool IsEnabled { get; }
    public bool IsHidden { get; }
}

public partial class SettingsSavingGoalItemVM : ObservableObject, ISettingsSelectableItem
{
    [ObservableProperty] private bool _isChecked;

    public SettingsSavingGoalItemVM(SavingGoal savingGoal, bool isHidden, bool isEnabled)
    {
        Id = savingGoal.Id;
        Name = savingGoal.Name;
        CurrentAmount = savingGoal.CurrentAmount;
        TargetAmount = savingGoal.TargetAmount;
        SavingEndDate = savingGoal.SavingEndDate;
        IsHidden = isHidden;
        IsEnabled = isEnabled;
    }

    public int Id { get; }
    public string Name { get; }
    public decimal CurrentAmount { get; }
    public decimal TargetAmount { get; }
    public DateTime SavingEndDate { get; }
    public bool IsHidden { get; }
    public bool IsEnabled { get; }
    public decimal ProgressRatio => TargetAmount <= 0 ? 0m : Math.Clamp(CurrentAmount / TargetAmount, 0m, 1m);
    public int ProgressPercentage => (int)Math.Round(ProgressRatio * 100m, MidpointRounding.AwayFromZero);
}
