using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Fluxo.Resources.Messages;

public readonly record struct StartupWizardNotificationsChanged(
    int EnabledCount,
    int TotalCount);

public sealed class StartupWizardNotificationsChangedMessage(StartupWizardNotificationsChanged value)
    : ValueChangedMessage<StartupWizardNotificationsChanged>(value);

