using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Constants;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.ViewModels.Messages;

namespace Fluxo.ViewModels.Shell;

public partial class MainVM : ObservableRecipient
{
    private readonly IUserSettingsRepository _userSettingsRepository;
    private bool _isInitialized;

    [ObservableProperty] private string _username = "User";

    public bool IsInitialized => _isInitialized;

    public MainVM(
        IUserSettingsRepository userSettingsRepository,
        NotificationPanelVM notificationPanel,
        BudgetAllocationPanelVM budgetPanel,
        SavingGoalsPanelVM savingGoalsPanel,
        DaySpinnerVM daySpinner,
        MainViewModeToggleVM viewModeToggle)
    {
        _userSettingsRepository = userSettingsRepository;
        NotificationPanel = notificationPanel;
        BudgetPanel = budgetPanel;
        SavingGoalsPanel = savingGoalsPanel;
        DaySpinner = daySpinner;
        ViewModeToggle = viewModeToggle;

        WeakReferenceMessenger.Default.Register<MainVM, UsernameChangedMessage>(this,
            static (recipient, message) => recipient.Username = message.Value);
        WeakReferenceMessenger.Default.Register<MainVM, ExpenseDetailUpdatedMessage>(this,
            static (recipient, message) => recipient.HandleExpenseDetailUpdatedMessage(message));
    }

    public NotificationPanelVM NotificationPanel { get; }
    public BudgetAllocationPanelVM BudgetPanel { get; }
    public SavingGoalsPanelVM SavingGoalsPanel { get; }
    public DaySpinnerVM DaySpinner { get; }
    public MainViewModeToggleVM ViewModeToggle { get; }

    public async Task Initialize()
    {
        await LoadUserSettingsAsync();
        await Task.WhenAll(
            BudgetPanel.LoadAsync(),
            NotificationPanel.LoadAsync(),
            SavingGoalsPanel.LoadAsync());
        ViewModeToggle.SetSelectedMainContentViewCommand.Execute(
            ViewModeToggle.SelectedMainContentViewMode);
        _isInitialized = true;
    }

    public async Task ReloadCurrentDataAsync()
    {
        await Task.WhenAll(
            BudgetPanel.LoadAsync(),
            NotificationPanel.LoadAsync(),
            SavingGoalsPanel.LoadAsync());
    }

    private async Task LoadUserSettingsAsync()
    {
        var settings = await _userSettingsRepository.GetAllAsync();
        var settingsByName = settings.ToDictionary(
            s => s.Name, s => s.Value, StringComparer.Ordinal);

        if (settingsByName.TryGetValue(UserSettingNames.PreferredDisplayName, out var name))
        {
            var trimmed = (name ?? string.Empty).Trim();
            Username = trimmed.Length > 0 ? trimmed : "User";
        }
    }
}
