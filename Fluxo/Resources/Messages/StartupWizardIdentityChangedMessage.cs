using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Fluxo.Resources.Messages;

public readonly record struct QuickSetupWizardIdentityChanged(
    string ResolvedUsername);

public sealed class QuickSetupWizardIdentityChangedMessage(QuickSetupWizardIdentityChanged value)
    : ValueChangedMessage<QuickSetupWizardIdentityChanged>(value);
