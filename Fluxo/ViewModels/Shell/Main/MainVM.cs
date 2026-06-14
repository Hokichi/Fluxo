using System.Collections.ObjectModel;
using System.ComponentModel;
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

    [ObservableProperty] private string _username = "User";

    public bool IsInitialized => _isInitialized;

    public MainVM(
        IDataOperationRunner dataOperationRunner,
        DashboardVM dashboard,
        Main.DaySpinnerVM daySpinner,
        Main.LedgerVM? ledger = null)
    {
        _dataOperationRunner = dataOperationRunner;
        Dashboard = dashboard;
        DaySpinner = daySpinner;
        Ledger = ledger;
        Dashboard.PropertyChanged += OnDashboardPropertyChanged;

        WeakReferenceMessenger.Default.Register<MainVM, UsernameChangedMessage>(this,
            static (recipient, message) => recipient.Username = message.Value);
        WeakReferenceMessenger.Default.Register<MainVM, ExpenseDetailUpdatedMessage>(this,
            static (recipient, message) => recipient.HandleExpenseDetailUpdatedMessage(message));
    }

    public DashboardVM Dashboard { get; }
    public Main.NotificationPanelVM NotificationPanel => Dashboard.NotificationPanel;
    public Main.BudgetAllocationPanelVM BudgetPanel => Dashboard.BudgetPanel;
    public Main.SpentAllowancePanelVM SpentAllowancePanel => Dashboard.SpentAllowancePanel;
    public Main.SavingGoalsPanelVM SavingGoalsPanel => Dashboard.SavingGoalsPanel;
    public Main.UpcomingEventsPanelVM UpcomingEventsPanel => Dashboard.UpcomingEventsPanel;
    public Main.DaySpinnerVM DaySpinner { get; }
    public Main.LedgerVM? Ledger { get; }

    public bool IsDashboardSpendingAmountGateLocked => Dashboard.IsDashboardSpendingAmountGateLocked;
    public bool IsSufficientFundsActionGateLocked => Dashboard.IsSufficientFundsActionGateLocked;

    public ObservableCollection<SpendingSourceVM> SpendingSources => Dashboard.SpendingSources;

    public void ToggleSpendingSourceFilter(SpendingSourceVM? spendingSource)
    {
        Dashboard.ToggleSpendingSourceFilter(spendingSource);
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

        await Dashboard.InitializeWithStartupStagesAsync(betweenStagesAsync);

        if (Ledger is not null)
        {
            await Ledger.LoadAsync();
            await betweenStagesAsync();
        }

        _isInitialized = true;
        OnPropertyChanged(nameof(IsInitialized));
    }

    public async Task ReloadCurrentDataAsync()
    {
        await Dashboard.ReloadCurrentDataAsync();

        if (Ledger is not null)
            await Ledger.LoadAsync();
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

    private void OnDashboardPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(DashboardVM.IsDashboardSpendingAmountGateLocked):
                OnPropertyChanged(nameof(IsDashboardSpendingAmountGateLocked));
                break;
            case nameof(DashboardVM.IsSufficientFundsActionGateLocked):
                OnPropertyChanged(nameof(IsSufficientFundsActionGateLocked));
                break;
        }
    }
}
