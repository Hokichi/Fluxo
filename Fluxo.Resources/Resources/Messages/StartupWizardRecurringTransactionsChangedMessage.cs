namespace Fluxo.Resources.Resources.Messages;

public readonly record struct QuickSetupWizardRecurringTransactionsChanged(
    int Count,
    decimal TotalAmount);

public sealed class QuickSetupWizardRecurringTransactionsChangedMessage(QuickSetupWizardRecurringTransactionsChanged value)
    : ValueChangedMessage<QuickSetupWizardRecurringTransactionsChanged>(value);


