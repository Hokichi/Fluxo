namespace Fluxo.Resources.Resources.Messages;

public readonly record struct QuickSetupWizardIdentityChanged(
    string ResolvedUsername);

public sealed class QuickSetupWizardIdentityChangedMessage(QuickSetupWizardIdentityChanged value)
    : ValueChangedMessage<QuickSetupWizardIdentityChanged>(value);
