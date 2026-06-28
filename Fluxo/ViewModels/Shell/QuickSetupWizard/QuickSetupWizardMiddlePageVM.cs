using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Resources.Resources.Messages;

namespace Fluxo.ViewModels.Shell.QuickSetupWizard;

public partial class QuickSetupWizardMiddlePageVM : ObservableRecipient,
    IRecipient<QuickSetupWizardAccountsChangedMessage>,
    IRecipient<QuickSetupWizardBudgetAllocationChangedMessage>
{
    private const int FirstMiddleStepIndex = 2;
    private const int LastMiddleStepIndex = 8;
    private const int MiddleStepsCount = LastMiddleStepIndex - FirstMiddleStepIndex + 1;
    private int _currentStepIndex = 2;

    [ObservableProperty] private bool _hasAccounts;
    [ObservableProperty] private bool _isStep2Active = true;
    [ObservableProperty] private bool _isStep3Active;
    [ObservableProperty] private bool _isStep4Active;
    [ObservableProperty] private bool _isStep5Active;
    [ObservableProperty] private bool _isStep6Active;
    [ObservableProperty] private bool _isStep7Active;
    [ObservableProperty] private bool _isStep8Active;

    public QuickSetupWizardMiddlePageVM(
        QuickSetupWizardAccountsVM accounts,
        QuickSetupWizardRecurringTransactionsVM fixedExpenses,
        QuickSetupWizardSavingGoalsVM savingGoals,
        QuickSetupWizardBudgetAllocationVM budgetAllocation,
        QuickSetupWizardPersonalizationVM personalization,
        QuickSetupWizardNotificationVM notification,
        QuickSetupWizardSummaryVM summary,
        IMessenger? messenger = null)
        : base(messenger ?? WeakReferenceMessenger.Default)
    {
        Accounts = accounts;
        RecurringTransactions = fixedExpenses;
        RecurringTransactions.SetAccounts(accounts);
        SavingGoals = savingGoals;
        BudgetAllocation = budgetAllocation;
        Personalization = personalization;
        Notification = notification;
        Summary = summary;
        IsActive = true;
    }

    public QuickSetupWizardAccountsVM Accounts { get; }

    public QuickSetupWizardRecurringTransactionsVM RecurringTransactions { get; }

    public QuickSetupWizardSavingGoalsVM SavingGoals { get; }

    public QuickSetupWizardBudgetAllocationVM BudgetAllocation { get; }

    public QuickSetupWizardPersonalizationVM Personalization { get; }

    public QuickSetupWizardNotificationVM Notification { get; }

    public QuickSetupWizardSummaryVM Summary { get; }

    public int MiddleStepCount => MiddleStepsCount;

    public int MiddleCurrentStep => IsMiddleStep ? _currentStepIndex - 1 : 1;

    public bool IsNextEnabled => !(_currentStepIndex == 5 && BudgetAllocation.HasBudgetAllocationError);

    public string StepCounterText => IsMiddleStep ? $"Step {MiddleCurrentStep} of {MiddleStepCount}" : string.Empty;

    public string CurrentStepTitle => _currentStepIndex switch
    {
        2 => "Add accounts",
        3 => "Add recurring transactions",
        4 => "Add savings goals",
        5 => "Budget allocation",
        6 => "Personalization",
        7 => "Preferences",
        _ => "Setup summary"
    };

    public string CurrentStepDescription => _currentStepIndex switch
    {
        2 => "Add the accounts and sources you spend from most often.",
        3 => "Add recurring transactions so fluxo can account for them upfront.",
        4 => "Add a few goals to start tracking progress right away.",
        5 => "Split your budget into Needs, Wants, and Invest.",
        6 => "Set how fluxo locks and unlocks when you're away.",
        7 => "Choose which reminders and alerts fluxo should show.",
        _ => "Here's a summary of everything you've set up."
    };

    private bool IsMiddleStep => _currentStepIndex is >= FirstMiddleStepIndex and <= LastMiddleStepIndex;

    public async Task LoadAsync()
    {
        await Accounts.RefreshAsync();
        await RecurringTransactions.RefreshAsync();
        await SavingGoals.RefreshAsync();
        await BudgetAllocation.LoadAsync();
        await Personalization.LoadAsync();
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
        IsStep8Active = stepIndex == 8;

        Accounts.IsStep2Active = IsStep2Active;
        RecurringTransactions.IsStep3Active = IsStep3Active;
        SavingGoals.IsStep4Active = IsStep4Active;
        BudgetAllocation.IsStep5Active = IsStep5Active;
        Personalization.IsStep6Active = IsStep6Active;
        Notification.IsStep7Active = IsStep7Active;
        Summary.IsStep8Active = IsStep8Active;

        OnPropertyChanged(nameof(MiddleCurrentStep));
        OnPropertyChanged(nameof(IsNextEnabled));
        OnPropertyChanged(nameof(StepCounterText));
        OnPropertyChanged(nameof(CurrentStepTitle));
        OnPropertyChanged(nameof(CurrentStepDescription));
    }

    public void Receive(QuickSetupWizardAccountsChangedMessage message)
    {
        HasAccounts = message.Value.HasAny;
    }

    public void Receive(QuickSetupWizardBudgetAllocationChangedMessage message)
    {
        OnPropertyChanged(nameof(IsNextEnabled));
    }
}

