namespace Fluxo.Resources.Resources.Messages;

public readonly record struct QuickSetupWizardAccountsChanged(
    int Count,
    bool HasAny,
    decimal TotalPrimaryAmount);

public sealed class QuickSetupWizardAccountsChangedMessage(QuickSetupWizardAccountsChanged value)
    : ValueChangedMessage<QuickSetupWizardAccountsChanged>(value);


