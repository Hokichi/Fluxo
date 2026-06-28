using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Resources.Resources.Messages;

namespace Fluxo.ViewModels.Shell.QuickSetupWizard;

public partial class QuickSetupWizardSummaryVM : ObservableRecipient,
    IRecipient<QuickSetupWizardIdentityChangedMessage>,
    IRecipient<QuickSetupWizardAccountsChangedMessage>,
    IRecipient<QuickSetupWizardRecurringTransactionsChangedMessage>,
    IRecipient<QuickSetupWizardSavingGoalsChangedMessage>,
    IRecipient<QuickSetupWizardBudgetAllocationChangedMessage>,
    IRecipient<QuickSetupWizardNotificationsChangedMessage>
{
    private int _investPercentage = 20;
    private int _needsPercentage = 50;
    private int _reportRecurringTransactionCount;
    private int _reportNotificationsEnabledCount;
    private int _reportSavingGoalCount;
    private int _reportAccountCount;
    private string _reportUsernameText = "User";
    private decimal _totalRecurringTransactionAmount;
    private decimal _totalPrimaryAmount;
    private int _wantsPercentage = 30;

    [ObservableProperty] private bool _isStep8Active;

    public QuickSetupWizardSummaryVM(IMessenger? messenger = null)
        : base(messenger ?? WeakReferenceMessenger.Default)
    {
        IsActive = true;
    }

    public string ReportUsernameText => _reportUsernameText;

    public int ReportAccountCount => _reportAccountCount;

    public int ReportRecurringTransactionCount => _reportRecurringTransactionCount;

    public int ReportSavingGoalCount => _reportSavingGoalCount;

    public string ReportTotalBalanceText =>
        MoneyFormatUtility.ToCompactText(_totalPrimaryAmount, CultureInfo.CurrentCulture);

    public string ReportTotalBalanceTooltipText =>
        MoneyFormatUtility.ToFullText(_totalPrimaryAmount, CultureInfo.CurrentCulture);

    public string ReportTotalRecurringTransactionText =>
        MoneyFormatUtility.ToCompactText(_totalRecurringTransactionAmount, CultureInfo.CurrentCulture);

    public string ReportTotalRecurringTransactionTooltipText =>
        MoneyFormatUtility.ToFullText(_totalRecurringTransactionAmount, CultureInfo.CurrentCulture);

    public string ReportBudgetAllocationText =>
        $"Needs {_needsPercentage}% / Wants {_wantsPercentage}% / Invest {_investPercentage}%";

    public int ReportNotificationsEnabledCount => _reportNotificationsEnabledCount;

    public void Receive(QuickSetupWizardIdentityChangedMessage message)
    {
        _reportUsernameText = message.Value.ResolvedUsername;
        OnPropertyChanged(nameof(ReportUsernameText));
    }

    public void Receive(QuickSetupWizardAccountsChangedMessage message)
    {
        _reportAccountCount = message.Value.Count;
        _totalPrimaryAmount = message.Value.TotalPrimaryAmount;
        OnPropertyChanged(nameof(ReportAccountCount));
        OnPropertyChanged(nameof(ReportTotalBalanceText));
        OnPropertyChanged(nameof(ReportTotalBalanceTooltipText));
    }

    public void Receive(QuickSetupWizardRecurringTransactionsChangedMessage message)
    {
        _reportRecurringTransactionCount = message.Value.Count;
        _totalRecurringTransactionAmount = message.Value.TotalAmount;
        OnPropertyChanged(nameof(ReportRecurringTransactionCount));
        OnPropertyChanged(nameof(ReportTotalRecurringTransactionText));
        OnPropertyChanged(nameof(ReportTotalRecurringTransactionTooltipText));
    }

    public void Receive(QuickSetupWizardSavingGoalsChangedMessage message)
    {
        _reportSavingGoalCount = message.Value.Count;
        OnPropertyChanged(nameof(ReportSavingGoalCount));
    }

    public void Receive(QuickSetupWizardBudgetAllocationChangedMessage message)
    {
        _needsPercentage = message.Value.NeedsPercentage;
        _wantsPercentage = message.Value.WantsPercentage;
        _investPercentage = message.Value.InvestPercentage;
        OnPropertyChanged(nameof(ReportBudgetAllocationText));
    }

    public void Receive(QuickSetupWizardNotificationsChangedMessage message)
    {
        _reportNotificationsEnabledCount = message.Value.EnabledCount;
        OnPropertyChanged(nameof(ReportNotificationsEnabledCount));
    }
}
