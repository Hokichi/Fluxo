using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Resources.Messages;

namespace Fluxo.ViewModels.Shell.QuickSetupWizard;

public partial class QuickSetupWizardMiddlePageVM : ObservableRecipient,
    IRecipient<QuickSetupWizardSpendingSourcesChangedMessage>,
    IRecipient<QuickSetupWizardBudgetAllocationChangedMessage>
{
    private const int FirstMiddleStepIndex = 2;
    private const int LastMiddleStepIndex = 7;
    private const int MiddleStepsCount = LastMiddleStepIndex - FirstMiddleStepIndex + 1;
    private int _currentStepIndex = 2;

    [ObservableProperty] private bool _hasSpendingSources;
    [ObservableProperty] private bool _isStep2Active = true;
    [ObservableProperty] private bool _isStep3Active;
    [ObservableProperty] private bool _isStep4Active;
    [ObservableProperty] private bool _isStep5Active;
    [ObservableProperty] private bool _isStep6Active;
    [ObservableProperty] private bool _isStep7Active;

    public QuickSetupWizardMiddlePageVM(
        QuickSetupWizardSpendingSourcesVM spendingSources,
        QuickSetupWizardFixedExpensesVM fixedExpenses,
        QuickSetupWizardSavingGoalsVM savingGoals,
        QuickSetupWizardBudgetAllocationVM budgetAllocation,
        QuickSetupWizardNotificationVM notification,
        QuickSetupWizardSummaryVM summary,
        IMessenger? messenger = null)
        : base(messenger ?? WeakReferenceMessenger.Default)
    {
        SpendingSources = spendingSources;
        FixedExpenses = fixedExpenses;
        FixedExpenses.SetSpendingSources(spendingSources);
        SavingGoals = savingGoals;
        BudgetAllocation = budgetAllocation;
        Notification = notification;
        Summary = summary;
        IsActive = true;
    }

    public QuickSetupWizardSpendingSourcesVM SpendingSources { get; }

    public QuickSetupWizardFixedExpensesVM FixedExpenses { get; }

    public QuickSetupWizardSavingGoalsVM SavingGoals { get; }

    public QuickSetupWizardBudgetAllocationVM BudgetAllocation { get; }

    public QuickSetupWizardNotificationVM Notification { get; }

    public QuickSetupWizardSummaryVM Summary { get; }

    public int MiddleStepCount => MiddleStepsCount;

    public int MiddleCurrentStep => IsMiddleStep ? _currentStepIndex - 1 : 1;

    public bool IsNextEnabled => !(_currentStepIndex == 5 && BudgetAllocation.HasBudgetAllocationError);

    public string StepCounterText => IsMiddleStep ? $"Step {MiddleCurrentStep} of {MiddleStepCount}" : string.Empty;

    public string CurrentStepTitle => _currentStepIndex switch
    {
        2 => "Add spending sources",
        3 => "Add fixed expenses",
        4 => "Add savings goals",
        5 => "Budget allocation",
        6 => "Preferences",
        _ => "Setup summary"
    };

    public string CurrentStepDescription => _currentStepIndex switch
    {
        2 => "Add the accounts and sources you spend from most often.",
        3 => "Add recurring fixed expenses so fluxo can account for them upfront.",
        4 => "Add a few goals to start tracking progress right away.",
        5 => "Split your budget into Needs, Wants, and Invest.",
        6 => "Choose which reminders and alerts fluxo should show.",
        _ => "Here's a summary of everything you've set up."
    };

    private bool IsMiddleStep => _currentStepIndex is >= FirstMiddleStepIndex and <= LastMiddleStepIndex;

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

        OnPropertyChanged(nameof(MiddleCurrentStep));
        OnPropertyChanged(nameof(IsNextEnabled));
        OnPropertyChanged(nameof(StepCounterText));
        OnPropertyChanged(nameof(CurrentStepTitle));
        OnPropertyChanged(nameof(CurrentStepDescription));
    }

    public void Receive(QuickSetupWizardSpendingSourcesChangedMessage message)
    {
        HasSpendingSources = message.Value.HasAny;
    }

    public void Receive(QuickSetupWizardBudgetAllocationChangedMessage message)
    {
        OnPropertyChanged(nameof(IsNextEnabled));
    }
}

