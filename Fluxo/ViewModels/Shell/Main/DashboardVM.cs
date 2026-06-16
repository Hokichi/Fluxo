using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.ViewModels.Entities;

namespace Fluxo.ViewModels.Shell.Main;

public partial class DashboardVM : ObservableObject
{
    private bool _isInitialized;

    [ObservableProperty] private bool _isDashboardSpendingAmountGateLocked;
    [ObservableProperty] private bool _isSufficientFundsActionGateLocked;

    public DashboardVM(
        NotificationPanelVM notificationPanel,
        BudgetAllocationPanelVM budgetPanel,
        SpentAllowancePanelVM spentAllowancePanel,
        SavingGoalsPanelVM savingGoalsPanel,
        UpcomingEventsPanelVM upcomingEventsPanel,
        MainViewModeToggleVM viewModeToggle,
        AllocationDataVM? allocationData = null)
    {
        NotificationPanel = notificationPanel;
        AllocationData = allocationData;
        BudgetPanel = budgetPanel;
        SpentAllowancePanel = spentAllowancePanel;
        SavingGoalsPanel = savingGoalsPanel;
        UpcomingEventsPanel = upcomingEventsPanel;
        ViewModeToggle = viewModeToggle;
    }

    public bool IsInitialized => _isInitialized;

    public NotificationPanelVM NotificationPanel { get; }
    public AllocationDataVM? AllocationData { get; }
    public BudgetAllocationPanelVM BudgetPanel { get; }
    public SpentAllowancePanelVM SpentAllowancePanel { get; }
    public SavingGoalsPanelVM SavingGoalsPanel { get; }
    public UpcomingEventsPanelVM UpcomingEventsPanel { get; }
    public MainViewModeToggleVM ViewModeToggle { get; }

    public ObservableCollection<SpendingSourceVM> SpendingSources => BudgetPanel.SpendingSources;

    public void ToggleSpendingSourceFilter(SpendingSourceVM? spendingSource)
    {
        BudgetPanel.ToggleSelectedSpendingSource(spendingSource);
    }

    public Task Initialize()
    {
        return InitializeWithStartupStagesAsync(static () => Task.CompletedTask);
    }

    public async Task InitializeWithStartupStagesAsync(Func<Task> betweenStagesAsync)
    {
        ArgumentNullException.ThrowIfNull(betweenStagesAsync);

        await BudgetPanel.LoadAsync();
        RefreshSpendingAmountGateStates();
        await betweenStagesAsync();

        if (AllocationData is not null)
        {
            await AllocationData.LoadAsync();
            await betweenStagesAsync();
        }

        await SpentAllowancePanel.LoadAsync();
        await betweenStagesAsync();

        await NotificationPanel.LoadAsync();
        await betweenStagesAsync();

        await SavingGoalsPanel.LoadAsync();
        await betweenStagesAsync();

        await UpcomingEventsPanel.LoadAsync();
        await betweenStagesAsync();

        ViewModeToggle.SetSelectedMainContentViewCommand.Execute(
            ViewModeToggle.SelectedMainContentViewMode);
        _isInitialized = true;
        OnPropertyChanged(nameof(IsInitialized));
    }

    public async Task ReloadCurrentDataAsync()
    {
        await BudgetPanel.LoadAsync();
        if (AllocationData is not null)
            await AllocationData.LoadAsync();
        RefreshSpendingAmountGateStates();

        await Task.WhenAll(
            SpentAllowancePanel.LoadAsync(),
            NotificationPanel.LoadAsync(),
            SavingGoalsPanel.LoadAsync(),
            UpcomingEventsPanel.LoadAsync());
    }

    public static bool ShouldLockDashboardForSpendingAmount(
        IEnumerable<SpendingSourceVM> spendingSources,
        IEnumerable<ExpenseLogVM> expenseLogs)
    {
        ArgumentNullException.ThrowIfNull(spendingSources);
        ArgumentNullException.ThrowIfNull(expenseLogs);

        return !spendingSources.Any(source => source.IsEnabled);
    }

    public static bool ShouldLockActionsForSufficientFunds(
        IEnumerable<SpendingSourceVM> spendingSources,
        IEnumerable<ExpenseLogVM> expenseLogs)
    {
        ArgumentNullException.ThrowIfNull(spendingSources);
        ArgumentNullException.ThrowIfNull(expenseLogs);

        var hasUsableFunds = spendingSources
            .Where(source => source.IsEnabled)
            .Any(HasUsableFunds);
        var hasActiveExpenseLogs = expenseLogs.Any(log => !log.IsForDeletion);

        return !hasUsableFunds && !hasActiveExpenseLogs;
    }

    private static bool HasUsableFunds(SpendingSourceVM source)
    {
        return source.SpendingSourceType is Fluxo.Core.Enums.SpendingSourceType.Credit or Fluxo.Core.Enums.SpendingSourceType.BNPL
            ? source.AccountLimit > 0m
            : source.Balance > 0m;
    }

    private void RefreshSpendingAmountGateStates()
    {
        IsDashboardSpendingAmountGateLocked = ShouldLockDashboardForSpendingAmount(
            SpendingSources,
            BudgetPanel.GetAllExpenseLogs());
        IsSufficientFundsActionGateLocked =
            IsDashboardSpendingAmountGateLocked ||
            ShouldLockActionsForSufficientFunds(SpendingSources, BudgetPanel.GetAllExpenseLogs());
    }
}
