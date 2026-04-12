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
    private const string DefaultCurrencyCode = "USD";

    private readonly MainVM _mainViewModel;
    private readonly Dictionary<string, bool> _savedNotificationSettings = new(StringComparer.Ordinal);
    private readonly Func<IUnitOfWork> _unitOfWorkFactory;

    [ObservableProperty] private string _budgetAllocationErrorMessage = string.Empty;
    [ObservableProperty] private int _investAllocationPercentage;
    [ObservableProperty] private bool _isFixedExpenseChecksEnabled;
    [ObservableProperty] private bool _isGoalChecksEnabled;
    [ObservableProperty] private bool _isSpendingSourceChecksEnabled;
    [ObservableProperty] private int _needsAllocationPercentage;
    [ObservableProperty] private string _preferredAppName = string.Empty;
    private BudgetAllocationSnapshot _savedBudgetAllocation = new(50, 30, 20);
    private string _savedCurrencyCode = DefaultCurrencyCode;
    private string _savedPreferredAppName = string.Empty;
    [ObservableProperty] private string _selectedCurrencyCode = DefaultCurrencyCode;
    [ObservableProperty] private int _wantsAllocationPercentage;

    public SettingsVM(MainVM mainViewModel, Func<IUnitOfWork> unitOfWorkFactory)
    {
        _mainViewModel = mainViewModel;
        _unitOfWorkFactory = unitOfWorkFactory;

        ReplaceCollection(CurrencyOptions, BuildCurrencyOptions());
        SelectedCurrencyCode = DefaultCurrencyCode;
    }

    public ObservableCollection<SettingsSpendingSourceItemVM> SpendingSources { get; } = [];
    public ObservableCollection<SettingsFixedExpenseItemVM> FixedExpenses { get; } = [];
    public ObservableCollection<SettingsSavingGoalItemVM> SavingGoals { get; } = [];
    public ObservableCollection<ExpenseTagVM> Tags { get; } = [];
    public ObservableCollection<SettingsNotificationOptionVM> NotificationSettings { get; } = [];
    public ObservableCollection<SettingsCurrencyOptionVM> CurrencyOptions { get; } = [];

    public decimal TotalBudgetAmount => _mainViewModel.TotalIncomeAmount;

    public bool HasBudgetAllocationError => !string.IsNullOrWhiteSpace(BudgetAllocationErrorMessage);

    public bool HasPendingConfigurationChanges =>
        NeedsAllocationPercentage != _savedBudgetAllocation.Needs ||
        WantsAllocationPercentage != _savedBudgetAllocation.Wants ||
        InvestAllocationPercentage != _savedBudgetAllocation.Invest ||
        !string.Equals((PreferredAppName ?? string.Empty).Trim(), _savedPreferredAppName, StringComparison.Ordinal) ||
        !string.Equals(SelectedCurrencyCode, _savedCurrencyCode, StringComparison.Ordinal) ||
        NotificationSettings.Any(setting =>
            _savedNotificationSettings.TryGetValue(setting.SettingName, out var savedValue)
                ? savedValue != setting.IsEnabled
                : setting.IsEnabled);

    public string NeedsAllocationAmountText => BuildAllocationAmountText(NeedsAllocationPercentage);
    public string WantsAllocationAmountText => BuildAllocationAmountText(WantsAllocationPercentage);
    public string InvestAllocationAmountText => BuildAllocationAmountText(InvestAllocationPercentage);
    public bool AreAllSpendingSourcesChecked => SpendingSources.Count > 0 && SpendingSources.All(item => item.IsChecked);
    public bool AreAllFixedExpensesChecked => FixedExpenses.Count > 0 && FixedExpenses.All(item => item.IsChecked);
    public bool AreAllGoalsChecked => SavingGoals.Count > 0 && SavingGoals.All(item => item.IsChecked);
    public bool ShowSpendingSourceHideActionButton =>
        IsSpendingSourceChecksEnabled && ShouldShowHideAction(SpendingSources);

    public bool ShowSpendingSourceUnhideActionButton =>
        IsSpendingSourceChecksEnabled && !ShouldShowHideAction(SpendingSources);

    public bool ShowSpendingSourceDisableActionButton =>
        IsSpendingSourceChecksEnabled && ShouldShowDisableAction(SpendingSources);

    public bool ShowSpendingSourceEnableActionButton =>
        IsSpendingSourceChecksEnabled && !ShouldShowDisableAction(SpendingSources);

    public bool ShowFixedExpenseHideActionButton =>
        IsFixedExpenseChecksEnabled && ShouldShowHideAction(FixedExpenses);

    public bool ShowFixedExpenseUnhideActionButton =>
        IsFixedExpenseChecksEnabled && !ShouldShowHideAction(FixedExpenses);

    public bool ShowFixedExpenseDisableActionButton =>
        IsFixedExpenseChecksEnabled && ShouldShowDisableAction(FixedExpenses);

    public bool ShowFixedExpenseEnableActionButton =>
        IsFixedExpenseChecksEnabled && !ShouldShowDisableAction(FixedExpenses);

    public bool ShowGoalHideActionButton =>
        IsGoalChecksEnabled && ShouldShowHideAction(SavingGoals);

    public bool ShowGoalUnhideActionButton =>
        IsGoalChecksEnabled && !ShouldShowHideAction(SavingGoals);

    public bool ShowGoalDisableActionButton =>
        IsGoalChecksEnabled && ShouldShowDisableAction(SavingGoals);

    public bool ShowGoalEnableActionButton =>
        IsGoalChecksEnabled && !ShouldShowDisableAction(SavingGoals);
    public bool ShowSpendingSourceCheckAllButton => IsSpendingSourceChecksEnabled && !AreAllSpendingSourcesChecked;
    public bool ShowSpendingSourceUncheckAllButton => IsSpendingSourceChecksEnabled && AreAllSpendingSourcesChecked;
    public bool ShowFixedExpenseCheckAllButton => IsFixedExpenseChecksEnabled && !AreAllFixedExpensesChecked;
    public bool ShowFixedExpenseUncheckAllButton => IsFixedExpenseChecksEnabled && AreAllFixedExpensesChecked;
    public bool ShowGoalCheckAllButton => IsGoalChecksEnabled && !AreAllGoalsChecked;
    public bool ShowGoalUncheckAllButton => IsGoalChecksEnabled && AreAllGoalsChecked;

    public string SelectedCurrencySymbol =>
        CurrencyOptions.FirstOrDefault(option =>
            string.Equals(option.Code, SelectedCurrencyCode, StringComparison.OrdinalIgnoreCase))?.Symbol ?? "$";

    partial void OnNeedsAllocationPercentageChanged(int value)
    {
        OnAllocationChanged();
    }

    partial void OnWantsAllocationPercentageChanged(int value)
    {
        OnAllocationChanged();
    }

    partial void OnInvestAllocationPercentageChanged(int value)
    {
        OnAllocationChanged();
    }

    partial void OnPreferredAppNameChanged(string value)
    {
        OnPropertyChanged(nameof(HasPendingConfigurationChanges));
    }

    partial void OnIsSpendingSourceChecksEnabledChanged(bool value)
    {
        OnSelectionStateChanged();
    }

    partial void OnIsFixedExpenseChecksEnabledChanged(bool value)
    {
        OnSelectionStateChanged();
    }

    partial void OnIsGoalChecksEnabledChanged(bool value)
    {
        OnSelectionStateChanged();
    }

    partial void OnSelectedCurrencyCodeChanged(string value)
    {
        if (CurrencyOptions.All(option => !string.Equals(option.Code, value, StringComparison.OrdinalIgnoreCase)))
        {
            var fallbackCode = CurrencyOptions.FirstOrDefault()?.Code ?? DefaultCurrencyCode;
            if (!string.Equals(SelectedCurrencyCode, fallbackCode, StringComparison.Ordinal))
                SelectedCurrencyCode = fallbackCode;
            return;
        }

        OnPropertyChanged(nameof(SelectedCurrencySymbol));
        OnPropertyChanged(nameof(NeedsAllocationAmountText));
        OnPropertyChanged(nameof(WantsAllocationAmountText));
        OnPropertyChanged(nameof(InvestAllocationAmountText));
        OnPropertyChanged(nameof(HasPendingConfigurationChanges));
    }

    public async Task LoadAsync()
    {
        await using var unitOfWork = _unitOfWorkFactory();

        var settings = await unitOfWork.UserSettings.GetAllAsync();
        var settingsByName =
            settings.ToDictionary(setting => setting.Name, setting => setting.Value, StringComparer.Ordinal);
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
        PreferredAppName = ParseString(settingsByName, UserSettingNames.PreferredDisplayName, string.Empty);
        _savedPreferredAppName = (PreferredAppName ?? string.Empty).Trim();
        SelectedCurrencyCode =
            ParseCurrencyCode(settingsByName, UserSettingNames.PreferredCurrencyCode, DefaultCurrencyCode);
        _savedCurrencyCode = SelectedCurrencyCode;

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
        AttachSelectableItemHandlers(SpendingSources);
        AttachSelectableItemHandlers(FixedExpenses);
        AttachSelectableItemHandlers(SavingGoals);
        ValidateBudgetAllocation();
        OnPropertyChanged(nameof(TotalBudgetAmount));
        OnPropertyChanged(nameof(SelectedCurrencySymbol));
        OnPropertyChanged(nameof(NeedsAllocationAmountText));
        OnPropertyChanged(nameof(WantsAllocationAmountText));
        OnPropertyChanged(nameof(InvestAllocationAmountText));
        OnPropertyChanged(nameof(HasPendingConfigurationChanges));
        OnSelectionStateChanged();
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
        await UpdateUserSettingAsync(unitOfWork, UserSettingNames.PreferredDisplayName,
            string.IsNullOrWhiteSpace(PreferredAppName) ? null : PreferredAppName.Trim(), actions);
        await UpdateUserSettingAsync(unitOfWork, UserSettingNames.PreferredCurrencyCode,
            SelectedCurrencyCode, actions);

        foreach (var notificationSetting in NotificationSettings)
            await UpdateUserSettingAsync(unitOfWork, notificationSetting.SettingName,
                notificationSetting.IsEnabled.ToString(CultureInfo.InvariantCulture), actions);

        if (actions.Count == 0)
            return SettingsOperationResult.Success();

        await unitOfWork.SaveChangesAsync();
        RecordActions(actions);
        await _mainViewModel.ReloadCurrentDataAsync(true);
        await LoadAsync();

        return SettingsOperationResult.Success();
    }

    public void RevertConfigurationChanges()
    {
        NeedsAllocationPercentage = _savedBudgetAllocation.Needs;
        WantsAllocationPercentage = _savedBudgetAllocation.Wants;
        InvestAllocationPercentage = _savedBudgetAllocation.Invest;
        PreferredAppName = _savedPreferredAppName;
        SelectedCurrencyCode = _savedCurrencyCode;

        foreach (var notificationSetting in NotificationSettings)
            if (_savedNotificationSettings.TryGetValue(notificationSetting.SettingName, out var savedValue))
                notificationSetting.IsEnabled = savedValue;

        ValidateBudgetAllocation();
        OnPropertyChanged(nameof(HasPendingConfigurationChanges));
    }

    public void ClearSelections(SettingsBatchTarget target)
    {
        SetSelections(target, false);
    }

    public void SetSelections(SettingsBatchTarget target, bool isChecked)
    {
        foreach (var item in GetSelectableItems(target))
            item.IsChecked = isChecked;
    }

    public bool ShouldWarnBeforeApplyingToAll(SettingsBatchTarget target, SettingsBatchAction action)
    {
        if (action is not (SettingsBatchAction.Hide or SettingsBatchAction.Disable))
            return false;

        var selectableItems = GetSelectableItems(target).ToArray();
        if (selectableItems.Length == 0)
            return false;

        var selectedCount = selectableItems.Count(item => item.IsChecked);
        return selectedCount == selectableItems.Length;
    }

    public async Task<SettingsOperationResult> ExecuteSpendingSourceActionAsync(
        SettingsBatchAction action,
        IReadOnlyCollection<int>? selectedIdsOverride = null)
    {
        var selectedIds = NormalizeSelectionIds(selectedIdsOverride, SpendingSources.Select(item => item.Id),
            SpendingSources.Where(item => item.IsChecked).Select(item => item.Id));
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
            await _mainViewModel.ReloadCurrentDataAsync(true);
            await LoadAsync();

            return SettingsOperationResult.Success();
        }
        catch (Exception exception)
        {
            return SettingsOperationResult.Failure(
                $"Unable to update the selected spending sources.\n\n{exception.Message}");
        }
    }

    public async Task<SettingsOperationResult> ExecuteFixedExpenseActionAsync(
        SettingsBatchAction action,
        IReadOnlyCollection<int>? selectedIdsOverride = null)
    {
        var selectedIds = NormalizeSelectionIds(selectedIdsOverride, FixedExpenses.Select(item => item.Id),
            FixedExpenses.Where(item => item.IsChecked).Select(item => item.Id));
        var selectedItemIds = selectedIds.ToHashSet();
        var selectedItems = FixedExpenses.Where(item => selectedItemIds.Contains(item.Id)).ToArray();
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
            await _mainViewModel.ReloadCurrentDataAsync(true);
            await LoadAsync();

            return SettingsOperationResult.Success();
        }
        catch (Exception exception)
        {
            return SettingsOperationResult.Failure(
                $"Unable to update the selected fixed expenses.\n\n{exception.Message}");
        }
    }

    public async Task<SettingsOperationResult> ExecuteGoalActionAsync(
        SettingsBatchAction action,
        IReadOnlyCollection<int>? selectedIdsOverride = null)
    {
        var selectedIds = NormalizeSelectionIds(selectedIdsOverride, SavingGoals.Select(item => item.Id),
            SavingGoals.Where(item => item.IsChecked).Select(item => item.Id));
        var selectedItemIds = selectedIds.ToHashSet();
        var selectedItems = SavingGoals.Where(item => selectedItemIds.Contains(item.Id)).ToArray();
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
            await _mainViewModel.ReloadCurrentDataAsync(true);
            await LoadAsync();

            return SettingsOperationResult.Success();
        }
        catch (Exception exception)
        {
            return SettingsOperationResult.Failure(
                $"Unable to update the selected goals.\n\n{exception.Message}");
        }
    }

    public Task<SettingsOperationResult> ExecuteSpendingSourceItemActionAsync(int itemId, SettingsBatchAction action)
    {
        return ExecuteSpendingSourceActionAsync(action, [itemId]);
    }

    public Task<SettingsOperationResult> ExecuteFixedExpenseItemActionAsync(int itemId, SettingsBatchAction action)
    {
        return ExecuteFixedExpenseActionAsync(action, [itemId]);
    }

    public Task<SettingsOperationResult> ExecuteGoalItemActionAsync(int itemId, SettingsBatchAction action)
    {
        return ExecuteGoalActionAsync(action, [itemId]);
    }

    public async Task<SettingsOperationResult> CreateTagAsync(string name, string hexCode)
    {
        var trimmedName = (name ?? string.Empty).Trim();
        var normalizedHexCode = NormalizeHexColor(hexCode);

        if (trimmedName.Length == 0)
            return SettingsOperationResult.Failure("Please enter a tag name.");

        if (!IsHexColor(normalizedHexCode))
            return SettingsOperationResult.Failure("Please choose a valid tag color.");

        await using var unitOfWork = _unitOfWorkFactory();

        try
        {
            var existingTags = await unitOfWork.ExpenseTags.GetAllAsync();
            if (existingTags.Any(tag => string.Equals(tag.Name, trimmedName, StringComparison.OrdinalIgnoreCase)))
                return SettingsOperationResult.Failure($"A tag named \"{trimmedName}\" already exists.");

            await unitOfWork.ExpenseTags.AddAsync(new ExpenseTag
            {
                Name = trimmedName,
                HexCode = normalizedHexCode
            });

            await unitOfWork.SaveChangesAsync();
            await _mainViewModel.ReloadCurrentDataAsync(true);
            await LoadAsync();

            return SettingsOperationResult.Success();
        }
        catch (Exception exception)
        {
            return SettingsOperationResult.Failure(
                $"Unable to create this tag.\n\n{exception.Message}");
        }
    }

    public async Task<SettingsOperationResult> ResetAllSettingsAsync()
    {
        await using var unitOfWork = _unitOfWorkFactory();

        try
        {
            var settings = await unitOfWork.UserSettings.GetAllAsync();
            var actions = new List<ILogMemoryAction>();

            foreach (var setting in settings)
            {
                unitOfWork.UserSettings.Remove(setting);
                actions.Add(new SetUserSettingMemoryAction(
                    UserSettingMemorySnapshot.Create(setting),
                    UserSettingMemorySnapshot.Missing(setting.Name)));
            }

            if (settings.Count > 0)
            {
                await unitOfWork.SaveChangesAsync();
                RecordActions(actions);
            }

            await _mainViewModel.ReloadCurrentDataAsync(true);
            await LoadAsync();

            return SettingsOperationResult.Success();
        }
        catch (Exception exception)
        {
            return SettingsOperationResult.Failure(
                $"Unable to reset settings.\n\n{exception.Message}");
        }
    }

    public async Task<SettingsOperationResult> DeleteAllDataAsync(bool keepSettings)
    {
        await using var unitOfWork = _unitOfWorkFactory();

        try
        {
            var expenseLogs = await unitOfWork.ExpenseLogs.GetAllAsync();
            var incomeLogs = await unitOfWork.IncomeLogs.GetAllAsync();
            var expenses = await unitOfWork.Expenses.GetAllAsync();
            var savingGoals = await unitOfWork.SavingGoals.GetAllAsync();
            var spendingSources = await unitOfWork.SpendingSources.GetAllAsync();
            var tags = await unitOfWork.ExpenseTags.GetAllAsync();
            var settings = keepSettings
                ? []
                : await unitOfWork.UserSettings.GetAllAsync();

            foreach (var setting in settings)
                unitOfWork.UserSettings.Remove(setting);

            foreach (var tag in tags)
                unitOfWork.ExpenseTags.Remove(tag);

            foreach (var spendingSource in spendingSources)
                unitOfWork.SpendingSources.Remove(spendingSource);

            foreach (var expense in expenses)
                unitOfWork.Expenses.Remove(expense);

            foreach (var expenseLog in expenseLogs)
                unitOfWork.ExpenseLogs.Remove(expenseLog);

            foreach (var incomeLog in incomeLogs)
                unitOfWork.IncomeLogs.Remove(incomeLog);

            foreach (var savingGoal in savingGoals)
                unitOfWork.SavingGoals.Remove(savingGoal);

            if (!keepSettings)
                await unitOfWork.UserSettings.AddAsync(new UserSettings
                {
                    Name = UserSettingNames.IsFirstRun,
                    Value = bool.TrueString
                });

            await unitOfWork.SaveChangesAsync();
            await _mainViewModel.ReloadCurrentDataAsync(true);
            await LoadAsync();

            return SettingsOperationResult.Success();
        }
        catch (Exception exception)
        {
            return SettingsOperationResult.Failure(
                $"Unable to delete all data.\n\n{exception.Message}");
        }
    }

    public AddSpendingSourceVM CreateAddSpendingSourceViewModel()
    {
        return new AddSpendingSourceVM(_mainViewModel, _unitOfWorkFactory);
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
            await _mainViewModel.ReloadCurrentDataAsync(true);
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
        var symbol = SelectedCurrencySymbol;
        return
            $"{symbol}{allocatedAmount.ToString("N2", CultureInfo.InvariantCulture)}";
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

    private static int[] NormalizeSelectionIds(IReadOnlyCollection<int>? selectedIdsOverride,
        IEnumerable<int> validIds,
        IEnumerable<int> fallbackIds)
    {
        var validIdSet = validIds.ToHashSet();
        var source = selectedIdsOverride is { Count: > 0 } ? selectedIdsOverride : fallbackIds;

        return source
            .Where(id => id > 0 && validIdSet.Contains(id))
            .Distinct()
            .ToArray();
    }

    private static IReadOnlyList<SettingsCurrencyOptionVM> BuildCurrencyOptions()
    {
        return
        [
            new SettingsCurrencyOptionVM("USD", "US Dollar", "$"),
            new SettingsCurrencyOptionVM("EUR", "Euro", "€"),
            new SettingsCurrencyOptionVM("GBP", "British Pound", "£"),
            new SettingsCurrencyOptionVM("JPY", "Japanese Yen", "¥"),
            new SettingsCurrencyOptionVM("THB", "Thai Baht", "฿"),
            new SettingsCurrencyOptionVM("AUD", "Australian Dollar", "A$"),
            new SettingsCurrencyOptionVM("CAD", "Canadian Dollar", "C$"),
            new SettingsCurrencyOptionVM("SGD", "Singapore Dollar", "S$"),
            new SettingsCurrencyOptionVM("VND", "Vietnamese Dong", "₫"),
            new SettingsCurrencyOptionVM("INR", "Indian Rupee", "₹")
        ];
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();
        foreach (var item in items)
            collection.Add(item);
    }

    private static string ParseString(IReadOnlyDictionary<string, string> settings, string name, string defaultValue)
    {
        if (!settings.TryGetValue(name, out var value))
            return defaultValue;

        return value?.Trim() ?? defaultValue;
    }

    private static int ParsePercentage(IReadOnlyDictionary<string, string> settings, string name, decimal defaultValue)
    {
        if (!settings.TryGetValue(name, out var value) ||
            !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedValue))
            return (int)defaultValue;

        return (int)Math.Round(parsedValue, MidpointRounding.AwayFromZero);
    }

    private string ParseCurrencyCode(IReadOnlyDictionary<string, string> settings, string name, string defaultValue)
    {
        var code = ParseString(settings, name, defaultValue).ToUpperInvariant();
        if (CurrencyOptions.Any(option => string.Equals(option.Code, code, StringComparison.OrdinalIgnoreCase)))
            return code;

        return defaultValue;
    }

    private static bool ParseBool(IReadOnlyDictionary<string, string> settings, string name, bool defaultValue)
    {
        return settings.TryGetValue(name, out var value) && bool.TryParse(value, out var parsedValue)
            ? parsedValue
            : defaultValue;
    }

    private static string NormalizeHexColor(string hexCode)
    {
        var normalized = (hexCode ?? string.Empty).Trim().TrimStart('#').ToUpperInvariant();
        return $"#{normalized}";
    }

    private static bool IsHexColor(string hexCode)
    {
        var normalized = NormalizeHexColor(hexCode);
        return normalized.Length == 7 &&
               normalized[0] == '#' &&
               normalized.Skip(1).All(static character => char.IsDigit(character) ||
                                                          (character >= 'A' && character <= 'F'));
    }

    private static HashSet<int> ParseIdSet(IReadOnlyDictionary<string, string> settings, string name)
    {
        if (!settings.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
            return [];

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part =>
                int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : -1)
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

    private void AttachSelectableItemHandlers<T>(IEnumerable<T> items)
        where T : ISettingsSelectableItem, INotifyPropertyChanged
    {
        foreach (var item in items)
        {
            item.PropertyChanged -= OnSelectableItemPropertyChanged;
            item.PropertyChanged += OnSelectableItemPropertyChanged;
        }
    }

    private void OnSelectableItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ISettingsSelectableItem.IsChecked))
            OnSelectionStateChanged();
    }

    private void OnSelectionStateChanged()
    {
        OnPropertyChanged(nameof(AreAllSpendingSourcesChecked));
        OnPropertyChanged(nameof(AreAllFixedExpensesChecked));
        OnPropertyChanged(nameof(AreAllGoalsChecked));
        OnPropertyChanged(nameof(ShowSpendingSourceHideActionButton));
        OnPropertyChanged(nameof(ShowSpendingSourceUnhideActionButton));
        OnPropertyChanged(nameof(ShowSpendingSourceDisableActionButton));
        OnPropertyChanged(nameof(ShowSpendingSourceEnableActionButton));
        OnPropertyChanged(nameof(ShowFixedExpenseHideActionButton));
        OnPropertyChanged(nameof(ShowFixedExpenseUnhideActionButton));
        OnPropertyChanged(nameof(ShowFixedExpenseDisableActionButton));
        OnPropertyChanged(nameof(ShowFixedExpenseEnableActionButton));
        OnPropertyChanged(nameof(ShowGoalHideActionButton));
        OnPropertyChanged(nameof(ShowGoalUnhideActionButton));
        OnPropertyChanged(nameof(ShowGoalDisableActionButton));
        OnPropertyChanged(nameof(ShowGoalEnableActionButton));
        OnPropertyChanged(nameof(ShowSpendingSourceCheckAllButton));
        OnPropertyChanged(nameof(ShowSpendingSourceUncheckAllButton));
        OnPropertyChanged(nameof(ShowFixedExpenseCheckAllButton));
        OnPropertyChanged(nameof(ShowFixedExpenseUncheckAllButton));
        OnPropertyChanged(nameof(ShowGoalCheckAllButton));
        OnPropertyChanged(nameof(ShowGoalUncheckAllButton));
    }

    private static bool ShouldShowHideAction<T>(IReadOnlyCollection<T> items)
        where T : ISettingsSelectableItem
    {
        if (items.Count == 0)
            return true;

        var scopedItems = GetScopedItems(items);
        return scopedItems.Any(item => !item.IsHidden);
    }

    private static bool ShouldShowDisableAction<T>(IReadOnlyCollection<T> items)
        where T : ISettingsSelectableItem
    {
        if (items.Count == 0)
            return true;

        var scopedItems = GetScopedItems(items);
        return scopedItems.Any(item => item.IsEnabled);
    }

    private static IReadOnlyList<T> GetScopedItems<T>(IReadOnlyCollection<T> items)
        where T : ISettingsSelectableItem
    {
        var selectedItems = items.Where(item => item.IsChecked).ToArray();
        return selectedItems.Length > 0 ? selectedItems : items.ToArray();
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
    bool IsHidden { get; }
    bool IsEnabled { get; }
}

public readonly record struct BudgetAllocationSnapshot(int Needs, int Wants, int Invest);

public readonly record struct SettingsOperationResult(bool IsSuccess, string? ErrorMessage)
{
    public static SettingsOperationResult Success()
    {
        return new SettingsOperationResult(true, null);
    }

    public static SettingsOperationResult Failure(string? errorMessage)
    {
        return new SettingsOperationResult(false, errorMessage);
    }
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

public sealed record SettingsCurrencyOptionVM(string Code, string Name, string Symbol)
{
    public string DisplayName => $"{Name} ({Code})";
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
