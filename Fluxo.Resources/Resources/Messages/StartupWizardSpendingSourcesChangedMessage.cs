namespace Fluxo.Resources.Resources.Messages;

public readonly record struct QuickSetupWizardSpendingSourcesChanged(
    int Count,
    bool HasAny,
    decimal TotalPrimaryAmount);

public sealed class QuickSetupWizardSpendingSourcesChangedMessage(QuickSetupWizardSpendingSourcesChanged value)
    : ValueChangedMessage<QuickSetupWizardSpendingSourcesChanged>(value);

