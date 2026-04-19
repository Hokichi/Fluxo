using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Converters;
using Fluxo.Resources.Messages;

namespace Fluxo.ViewModels.Shell.StartupWizard;

public partial class StartupWizardSummaryVM : ObservableRecipient,
    IRecipient<StartupWizardIdentityChangedMessage>,
    IRecipient<StartupWizardSpendingSourcesChangedMessage>,
    IRecipient<StartupWizardFixedExpensesChangedMessage>,
    IRecipient<StartupWizardSavingGoalsChangedMessage>,
    IRecipient<StartupWizardBudgetAllocationChangedMessage>,
    IRecipient<StartupWizardNotificationsChangedMessage>
{
    private int _investPercentage = 20;
    private int _needsPercentage = 50;
    private string _reportCurrencyText = StartupWizardShared.DefaultCurrencyCode;
    private int _reportFixedExpenseCount;
    private int _reportNotificationsEnabledCount;
    private int _reportSavingGoalCount;
    private int _reportSpendingSourceCount;
    private string _reportUsernameText = "User";
    private decimal _totalFixedExpenseAmount;
    private decimal _totalPrimaryAmount;
    private int _wantsPercentage = 30;

    [ObservableProperty] private bool _isStep7Active;

    public StartupWizardSummaryVM(IMessenger? messenger = null)
        : base(messenger ?? WeakReferenceMessenger.Default)
    {
        IsActive = true;
    }

    public string ReportUsernameText => _reportUsernameText;

    public string ReportCurrencyText => _reportCurrencyText;

    public int ReportSpendingSourceCount => _reportSpendingSourceCount;

    public int ReportFixedExpenseCount => _reportFixedExpenseCount;

    public int ReportSavingGoalCount => _reportSavingGoalCount;

    public string ReportTotalBalanceText =>
        MoneyFormatUtility.ToCompactText(_totalPrimaryAmount, CultureInfo.CurrentCulture);

    public string ReportTotalBalanceTooltipText =>
        MoneyFormatUtility.ToFullText(_totalPrimaryAmount, CultureInfo.CurrentCulture);

    public string ReportTotalFixedExpenseText =>
        MoneyFormatUtility.ToCompactText(_totalFixedExpenseAmount, CultureInfo.CurrentCulture);

    public string ReportTotalFixedExpenseTooltipText =>
        MoneyFormatUtility.ToFullText(_totalFixedExpenseAmount, CultureInfo.CurrentCulture);

    public string ReportBudgetAllocationText =>
        $"Needs {_needsPercentage}% / Wants {_wantsPercentage}% / Invest {_investPercentage}%";

    public int ReportNotificationsEnabledCount => _reportNotificationsEnabledCount;

    public void Receive(StartupWizardIdentityChangedMessage message)
    {
        _reportUsernameText = message.Value.ResolvedUsername;
        _reportCurrencyText = message.Value.SelectedCurrencyCode;
        OnPropertyChanged(nameof(ReportUsernameText));
        OnPropertyChanged(nameof(ReportCurrencyText));
    }

    public void Receive(StartupWizardSpendingSourcesChangedMessage message)
    {
        _reportSpendingSourceCount = message.Value.Count;
        _totalPrimaryAmount = message.Value.TotalPrimaryAmount;
        OnPropertyChanged(nameof(ReportSpendingSourceCount));
        OnPropertyChanged(nameof(ReportTotalBalanceText));
        OnPropertyChanged(nameof(ReportTotalBalanceTooltipText));
    }

    public void Receive(StartupWizardFixedExpensesChangedMessage message)
    {
        _reportFixedExpenseCount = message.Value.Count;
        _totalFixedExpenseAmount = message.Value.TotalAmount;
        OnPropertyChanged(nameof(ReportFixedExpenseCount));
        OnPropertyChanged(nameof(ReportTotalFixedExpenseText));
        OnPropertyChanged(nameof(ReportTotalFixedExpenseTooltipText));
    }

    public void Receive(StartupWizardSavingGoalsChangedMessage message)
    {
        _reportSavingGoalCount = message.Value.Count;
        OnPropertyChanged(nameof(ReportSavingGoalCount));
    }

    public void Receive(StartupWizardBudgetAllocationChangedMessage message)
    {
        _needsPercentage = message.Value.NeedsPercentage;
        _wantsPercentage = message.Value.WantsPercentage;
        _investPercentage = message.Value.InvestPercentage;
        OnPropertyChanged(nameof(ReportBudgetAllocationText));
    }

    public void Receive(StartupWizardNotificationsChangedMessage message)
    {
        _reportNotificationsEnabledCount = message.Value.EnabledCount;
        OnPropertyChanged(nameof(ReportNotificationsEnabledCount));
    }
}

