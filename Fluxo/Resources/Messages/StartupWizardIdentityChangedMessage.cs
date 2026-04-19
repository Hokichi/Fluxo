using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Fluxo.Resources.Messages;

public readonly record struct StartupWizardIdentityChanged(
    string ResolvedUsername);

public sealed class StartupWizardIdentityChangedMessage(StartupWizardIdentityChanged value)
    : ValueChangedMessage<StartupWizardIdentityChanged>(value);
