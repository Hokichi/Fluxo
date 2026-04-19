using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
using Fluxo.Resources.Messages;
using Fluxo.Services.History;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Shell.Main;

namespace Fluxo.ViewModels.Popups.Settings;

public partial class SettingsVM : ObservableRecipient, IRecipient<SettingsPendingChangesChangedMessage>
{
    private readonly MainVM _mainViewModel;
    private readonly IUnitOfWork _unitOfWork;
    private bool _isBudgetPending;
    private bool _isPersonalizationPending;

    public SettingsVM(
        MainVM mainViewModel,
        IUnitOfWork unitOfWork,
        SettingsBudgetTabVM budgetTab,
        SettingsSourcesTabVM sourcesTab,
        SettingsFixedExpensesTabVM fixedExpensesTab,
        SettingsGoalsTabVM goalsTab,
        SettingsTagsTabVM tagsTab,
        SettingsPersonalizationTabVM personalizationTab,
        IMessenger? messenger = null)
        : base(messenger ?? WeakReferenceMessenger.Default)
    {
        _mainViewModel = mainViewModel;
        _unitOfWork = unitOfWork;
        BudgetTab = budgetTab;
        SourcesTab = sourcesTab;
        FixedExpensesTab = fixedExpensesTab;
        GoalsTab = goalsTab;
        TagsTab = tagsTab;
        PersonalizationTab = personalizationTab;
        IsActive = true;
    }

    public SettingsBudgetTabVM BudgetTab { get; }
    public SettingsSourcesTabVM SourcesTab { get; }
    public SettingsFixedExpensesTabVM FixedExpensesTab { get; }
    public SettingsGoalsTabVM GoalsTab { get; }
    public SettingsTagsTabVM TagsTab { get; }
    public SettingsPersonalizationTabVM PersonalizationTab { get; }

    public bool HasPendingConfigurationChanges => _isBudgetPending || _isPersonalizationPending;

    public void Receive(SettingsPendingChangesChangedMessage message)
    {
        switch (message.Value.TabKey)
        {
            case SettingsTabKey.Budget:
                _isBudgetPending = message.Value.HasPendingChanges;
                break;

            case SettingsTabKey.Personalization:
                _isPersonalizationPending = message.Value.HasPendingChanges;
                break;

            default:
                return;
        }

        OnPropertyChanged(nameof(HasPendingConfigurationChanges));
    }

    public async Task LoadAsync()
    {
        // Tab VMs share the same unit of work, so loads must be sequenced.
        await BudgetTab.LoadAsync();
        await SourcesTab.LoadAsync();
        await FixedExpensesTab.LoadAsync();
        await GoalsTab.LoadAsync();
        await TagsTab.LoadAsync();
        await PersonalizationTab.LoadAsync();

        OnPropertyChanged(nameof(IsSpendingSourceChecksEnabled));
        OnPropertyChanged(nameof(IsFixedExpenseChecksEnabled));
        OnPropertyChanged(nameof(IsGoalChecksEnabled));
        OnPropertyChanged(nameof(HasPendingConfigurationChanges));
    }

    public async Task<SettingsOperationResult> ApplyConfigurationAsync()
    {
        var (budgetResult, budgetActions) = await BudgetTab.BuildApplyChangesAsync();
        if (!budgetResult.IsSuccess)
            return budgetResult;

        var (personalResult, personalizationActions, oldUsername, newUsername) =
            await PersonalizationTab.BuildApplyChangesAsync();
        if (!personalResult.IsSuccess)
            return personalResult;

        var actions = new List<ILogMemoryAction>();
        actions.AddRange(budgetActions);
        actions.AddRange(personalizationActions);

        if (actions.Count == 0)
            return SettingsOperationResult.Success();

        await _unitOfWork.SaveChangesAsync();

        if (!string.Equals(newUsername, oldUsername, StringComparison.Ordinal))
            Messenger.Send(new UsernameChangedMessage(newUsername ?? "User"));

        SettingsShared.RecordActions(actions, Messenger);
        Messenger.Send(new DashboardDataInvalidatedMessage(
            DashboardDataInvalidationScope.All));
        await _mainViewModel.ReloadCurrentDataAsync();
        await LoadAsync();

        BudgetTab.CommitSavedState();
        PersonalizationTab.CommitSavedState();
        OnPropertyChanged(nameof(HasPendingConfigurationChanges));

        return SettingsOperationResult.Success();
    }

    public void RevertConfigurationChanges()
    {
        BudgetTab.RevertChanges();
        PersonalizationTab.RevertChanges();
        OnPropertyChanged(nameof(HasPendingConfigurationChanges));
    }

    public void ClearSelections(SettingsBatchTarget target)
    {
        switch (target)
        {
            case SettingsBatchTarget.SpendingSources:
                SourcesTab.ClearSelections();
                break;

            case SettingsBatchTarget.FixedExpenses:
                FixedExpensesTab.ClearSelections();
                break;

            case SettingsBatchTarget.Goals:
                GoalsTab.ClearSelections();
                break;
        }
    }

    public void SetSelections(SettingsBatchTarget target, bool isChecked)
    {
        switch (target)
        {
            case SettingsBatchTarget.SpendingSources:
                SourcesTab.SetSelections(isChecked);
                break;

            case SettingsBatchTarget.FixedExpenses:
                FixedExpensesTab.SetSelections(isChecked);
                break;

            case SettingsBatchTarget.Goals:
                GoalsTab.SetSelections(isChecked);
                break;
        }
    }

    public bool ShouldWarnBeforeApplyingToAll(SettingsBatchTarget target, SettingsBatchAction action)
    {
        return target switch
        {
            SettingsBatchTarget.SpendingSources => SourcesTab.ShouldWarnBeforeApplyingToAll(action),
            SettingsBatchTarget.FixedExpenses => FixedExpensesTab.ShouldWarnBeforeApplyingToAll(action),
            SettingsBatchTarget.Goals => GoalsTab.ShouldWarnBeforeApplyingToAll(action),
            _ => false
        };
    }

    public Task<SettingsOperationResult> ExecuteSpendingSourceActionAsync(SettingsBatchAction action,
        IReadOnlyCollection<int>? selectedIdsOverride = null)
    {
        return SourcesTab.ExecuteActionAsync(action, selectedIdsOverride);
    }

    public Task<SettingsOperationResult> ExecuteFixedExpenseActionAsync(SettingsBatchAction action,
        IReadOnlyCollection<int>? selectedIdsOverride = null)
    {
        return FixedExpensesTab.ExecuteActionAsync(action, selectedIdsOverride);
    }

    public Task<SettingsOperationResult> ExecuteGoalActionAsync(SettingsBatchAction action,
        IReadOnlyCollection<int>? selectedIdsOverride = null)
    {
        return GoalsTab.ExecuteActionAsync(action, selectedIdsOverride);
    }

    public Task<SettingsOperationResult> ExecuteSpendingSourceItemActionAsync(int itemId, SettingsBatchAction action)
    {
        return SourcesTab.ExecuteItemActionAsync(itemId, action);
    }

    public Task<SettingsOperationResult> ExecuteFixedExpenseItemActionAsync(int itemId, SettingsBatchAction action)
    {
        return FixedExpensesTab.ExecuteItemActionAsync(itemId, action);
    }

    public Task<SettingsOperationResult> ExecuteGoalItemActionAsync(int itemId, SettingsBatchAction action)
    {
        return GoalsTab.ExecuteItemActionAsync(itemId, action);
    }

    public Task<SettingsOperationResult> CreateTagAsync(string name, string hexCode)
    {
        return TagsTab.CreateTagAsync(name, hexCode);
    }

    public Task<SettingsOperationResult> DeleteTagAsync(ExpenseTagVM tag)
    {
        return TagsTab.DeleteTagAsync(tag);
    }

    public async Task<SettingsOperationResult> ResetAllSettingsAsync()
    {
        try
        {
            var settings = await _unitOfWork.UserSettings.GetAllAsync();
            var actions = new List<ILogMemoryAction>();

            foreach (var setting in settings)
            {
                _unitOfWork.UserSettings.Remove(setting);
                actions.Add(new SetUserSettingMemoryAction(
                    UserSettingMemorySnapshot.Create(setting),
                    UserSettingMemorySnapshot.Missing(setting.Name)));
            }

            if (settings.Count > 0)
            {
                await _unitOfWork.SaveChangesAsync();
                SettingsShared.RecordActions(actions, Messenger);
            }

            Messenger.Send(new DashboardDataInvalidatedMessage(
                DashboardDataInvalidationScope.All));
            await _mainViewModel.ReloadCurrentDataAsync();
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
        try
        {
            var expenseLogs = await _unitOfWork.ExpenseLogs.GetAllAsync();
            var incomeLogs = await _unitOfWork.IncomeLogs.GetAllAsync();
            var expenses = await _unitOfWork.Expenses.GetAllAsync();
            var savingGoals = await _unitOfWork.SavingGoals.GetAllAsync();
            var spendingSources = await _unitOfWork.SpendingSources.GetAllAsync();
            var tags = await _unitOfWork.ExpenseTags.GetAllAsync();
            var settings = keepSettings ? [] : await _unitOfWork.UserSettings.GetAllAsync();

            foreach (var tag in tags)
                _unitOfWork.ExpenseTags.Remove(tag);

            foreach (var source in spendingSources)
                _unitOfWork.SpendingSources.Remove(source);

            foreach (var expense in expenses)
                _unitOfWork.Expenses.Remove(expense);

            foreach (var expenseLog in expenseLogs)
                _unitOfWork.ExpenseLogs.Remove(expenseLog);

            foreach (var incomeLog in incomeLogs)
                _unitOfWork.IncomeLogs.Remove(incomeLog);

            foreach (var goal in savingGoals)
                _unitOfWork.SavingGoals.Remove(goal);

            if (!keepSettings)
                foreach (var setting in settings)
                    _unitOfWork.UserSettings.Remove(setting);

            await _unitOfWork.SaveChangesAsync();
            Messenger.Send(new DashboardDataInvalidatedMessage(
                DashboardDataInvalidationScope.All));
            await _mainViewModel.ReloadCurrentDataAsync();
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
        return SourcesTab.CreateAddSpendingSourceViewModel();
    }

    public AddFixedExpenseVM CreateAddFixedExpenseViewModel()
    {
        return FixedExpensesTab.CreateAddFixedExpenseViewModel();
    }

    public AddSavingGoalVM CreateAddSavingGoalViewModel()
    {
        return GoalsTab.CreateAddSavingGoalViewModel();
    }

    public SpendingSourceDetailVM CreateSpendingSourceDetailViewModel(int spendingSourceId)
    {
        return SourcesTab.CreateSpendingSourceDetailViewModel(spendingSourceId);
    }

    public void SelectSingleItem(SettingsBatchTarget target, int itemId)
    {
        switch (target)
        {
            case SettingsBatchTarget.SpendingSources:
                SourcesTab.SelectSingleItem(itemId);
                break;

            case SettingsBatchTarget.FixedExpenses:
                FixedExpensesTab.SelectSingleItem(itemId);
                break;

            case SettingsBatchTarget.Goals:
                GoalsTab.SelectSingleItem(itemId);
                break;
        }
    }

    public Task RefreshSpendingSourcesAsync()
    {
        return SourcesTab.RefreshSpendingSourcesAsync();
    }

    public Task RefreshFixedExpensesAsync()
    {
        return FixedExpensesTab.RefreshFixedExpensesAsync();
    }

    public Task RefreshSavingGoalsAsync()
    {
        return GoalsTab.RefreshSavingGoalsAsync();
    }

    public void IncrementAllocation(BudgetAllocationSegment segment, int delta)
    {
        BudgetTab.IncrementAllocation(segment, delta);
    }

    public void SetAllocation(BudgetAllocationSegment segment, double value)
    {
        BudgetTab.SetAllocation(segment, value);
    }

    public bool IsSpendingSourceChecksEnabled
    {
        get => SourcesTab.IsSpendingSourceChecksEnabled;
        set
        {
            SourcesTab.IsSpendingSourceChecksEnabled = value;
            OnPropertyChanged();
        }
    }

    public bool IsFixedExpenseChecksEnabled
    {
        get => FixedExpensesTab.IsFixedExpenseChecksEnabled;
        set
        {
            FixedExpensesTab.IsFixedExpenseChecksEnabled = value;
            OnPropertyChanged();
        }
    }

    public bool IsGoalChecksEnabled
    {
        get => GoalsTab.IsGoalChecksEnabled;
        set
        {
            GoalsTab.IsGoalChecksEnabled = value;
            OnPropertyChanged();
        }
    }
}