using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Constants;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.Resources.Resources.Messages;
using Fluxo.ViewModels.Entities;

namespace Fluxo.ViewModels.Shell.Main;

public partial class MainVM : ObservableRecipient
{
    private readonly IDataOperationRunner _dataOperationRunner;
    private bool _isInitialized;

    [ObservableProperty] private bool _isDashboardSpendingAmountGateLocked;
    [ObservableProperty] private string _username = "User";

    public bool IsInitialized => _isInitialized;

    public MainVM(
        IDataOperationRunner dataOperationRunner,
        Main.NotificationPanelVM notificationPanel,
        Main.BudgetAllocationPanelVM budgetPanel,
        Main.SpentAllowancePanelVM spentAllowancePanel,
        Main.SavingGoalsPanelVM savingGoalsPanel,
        Main.DaySpinnerVM daySpinner,
        Main.MainViewModeToggleVM viewModeToggle)
    {
        _dataOperationRunner = dataOperationRunner;
        NotificationPanel = notificationPanel;
        BudgetPanel = budgetPanel;
        SpentAllowancePanel = spentAllowancePanel;
        SavingGoalsPanel = savingGoalsPanel;
        DaySpinner = daySpinner;
        ViewModeToggle = viewModeToggle;

        WeakReferenceMessenger.Default.Register<MainVM, UsernameChangedMessage>(this,
            static (recipient, message) => recipient.Username = message.Value);
        WeakReferenceMessenger.Default.Register<MainVM, ExpenseDetailUpdatedMessage>(this,
            static (recipient, message) => recipient.HandleExpenseDetailUpdatedMessage(message));
    }

    public Main.NotificationPanelVM NotificationPanel { get; }
    public Main.BudgetAllocationPanelVM BudgetPanel { get; }
    public Main.SpentAllowancePanelVM SpentAllowancePanel { get; }
    public Main.SavingGoalsPanelVM SavingGoalsPanel { get; }
    public Main.DaySpinnerVM DaySpinner { get; }
    public Main.MainViewModeToggleVM ViewModeToggle { get; }

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

        await LoadUserSettingsAsync();
        await betweenStagesAsync();

        await BudgetPanel.LoadAsync();
        IsDashboardSpendingAmountGateLocked = ShouldLockDashboardForSpendingAmount(SpendingSources, BudgetPanel.GetAllExpenseLogs());
        await betweenStagesAsync();

        await SpentAllowancePanel.LoadAsync();
        await betweenStagesAsync();

        await NotificationPanel.LoadAsync();
        await betweenStagesAsync();

        await SavingGoalsPanel.LoadAsync();
        await betweenStagesAsync();

        ViewModeToggle.SetSelectedMainContentViewCommand.Execute(
            ViewModeToggle.SelectedMainContentViewMode);
        _isInitialized = true;
    }

    public async Task ReloadCurrentDataAsync()
    {
        await BudgetPanel.LoadAsync();
        IsDashboardSpendingAmountGateLocked = ShouldLockDashboardForSpendingAmount(SpendingSources, BudgetPanel.GetAllExpenseLogs());

        await Task.WhenAll(
            SpentAllowancePanel.LoadAsync(),
            NotificationPanel.LoadAsync(),
            SavingGoalsPanel.LoadAsync());
    }

    public static bool ShouldLockDashboardForSpendingAmount(
        IEnumerable<SpendingSourceVM> spendingSources,
        IEnumerable<ExpenseLogVM> expenseLogs)
    {
        ArgumentNullException.ThrowIfNull(spendingSources);
        ArgumentNullException.ThrowIfNull(expenseLogs);

        return !spendingSources.Any(source => source.IsEnabled);
    }

    private async Task LoadUserSettingsAsync()
    {
        var settingsByName = await _dataOperationRunner.RunAsync(async (scope, ct) =>
        {
            var settings = await scope.UnitOfWork.UserSettings.GetAllAsync(ct);
            return settings.ToDictionary(s => s.Name, s => s.Value, StringComparer.Ordinal);
        });

        if (settingsByName.TryGetValue(UserSettingNames.PreferredDisplayName, out var name))
        {
            var trimmed = (name ?? string.Empty).Trim();
            Username = trimmed.Length > 0 ? trimmed : "User";
        }
    }

    private void HandleExpenseDetailUpdatedMessage(ExpenseDetailUpdatedMessage message)
    {
        if (!message.Value.HasChanges)
            return;

        _ = ReloadCurrentDataAsync();
    }
}
