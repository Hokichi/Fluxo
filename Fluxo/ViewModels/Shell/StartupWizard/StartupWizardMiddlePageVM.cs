using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Resources.Messages;

namespace Fluxo.ViewModels.Shell.StartupWizard;

public partial class StartupWizardMiddlePageVM : ObservableRecipient,
    IRecipient<StartupWizardSpendingSourcesChangedMessage>
{
    private int _currentStepIndex = 2;

    [ObservableProperty] private bool _hasSpendingSources;
    [ObservableProperty] private bool _isStep2Active = true;
    [ObservableProperty] private bool _isStep3Active;
    [ObservableProperty] private bool _isStep4Active;
    [ObservableProperty] private bool _isStep5Active;
    [ObservableProperty] private bool _isStep6Active;
    [ObservableProperty] private bool _isStep7Active;

    public StartupWizardMiddlePageVM(
        StartupWizardSpendingSourcesVM spendingSources,
        StartupWizardFixedExpensesVM fixedExpenses,
        StartupWizardSavingGoalsVM savingGoals,
        StartupWizardBudgetAllocationVM budgetAllocation,
        StartupWizardNotificationVM notification,
        StartupWizardSummaryVM summary,
        IMessenger? messenger = null)
        : base(messenger ?? WeakReferenceMessenger.Default)
    {
        SpendingSources = spendingSources;
        FixedExpenses = fixedExpenses;
        SavingGoals = savingGoals;
        BudgetAllocation = budgetAllocation;
        Notification = notification;
        Summary = summary;
        IsActive = true;
    }

    public StartupWizardSpendingSourcesVM SpendingSources { get; }

    public StartupWizardFixedExpensesVM FixedExpenses { get; }

    public StartupWizardSavingGoalsVM SavingGoals { get; }

    public StartupWizardBudgetAllocationVM BudgetAllocation { get; }

    public StartupWizardNotificationVM Notification { get; }

    public StartupWizardSummaryVM Summary { get; }

    public string StepCounterText => IsMiddleStep ? $"Step {_currentStepIndex - 1} of 6" : string.Empty;

    public string CurrentStepTitle => _currentStepIndex switch
    {
        2 => "Add spending sources",
        3 => "Add fixed expenses",
        4 => "Add savings goals",
        5 => "Budget allocation",
        6 => "Notification preferences",
        _ => "Setup summary"
    };

    public string CurrentStepDescription => _currentStepIndex switch
    {
        2 => "Add the accounts and sources you spend from most often.",
        3 => "Add recurring fixed expenses so Fluxo can account for them upfront.",
        4 => "Add a few goals to start tracking progress right away.",
        5 => "Split your budget into Needs, Wants, and Invest.",
        6 => "Choose which reminders and alerts Fluxo should show.",
        _ => "Here's a summary of everything you've set up."
    };

    private bool IsMiddleStep => _currentStepIndex is >= 2 and <= 7;

    public async Task LoadAsync()
    {
        await SpendingSources.RefreshAsync();
        await FixedExpenses.RefreshAsync();
        await SavingGoals.RefreshAsync();
        await BudgetAllocation.LoadAsync();
        await Notification.LoadAsync();
    }

    public void SetCurrentStepIndex(int stepIndex)
    {
        _currentStepIndex = stepIndex;

        IsStep2Active = stepIndex == 2;
        IsStep3Active = stepIndex == 3;
        IsStep4Active = stepIndex == 4;
        IsStep5Active = stepIndex == 5;
        IsStep6Active = stepIndex == 6;
        IsStep7Active = stepIndex == 7;

        SpendingSources.IsStep2Active = IsStep2Active;
        FixedExpenses.IsStep3Active = IsStep3Active;
        SavingGoals.IsStep4Active = IsStep4Active;
        BudgetAllocation.IsStep5Active = IsStep5Active;
        Notification.IsStep6Active = IsStep6Active;
        Summary.IsStep7Active = IsStep7Active;

        OnPropertyChanged(nameof(StepCounterText));
        OnPropertyChanged(nameof(CurrentStepTitle));
        OnPropertyChanged(nameof(CurrentStepDescription));
    }

    public void Receive(StartupWizardSpendingSourcesChangedMessage message)
    {
        HasSpendingSources = message.Value.HasAny;
    }
}

