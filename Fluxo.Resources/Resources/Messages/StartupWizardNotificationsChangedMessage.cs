namespace Fluxo.Resources.Resources.Messages;

public readonly record struct QuickSetupWizardNotificationsChanged(
    int EnabledCount,
    int TotalCount);

public sealed class QuickSetupWizardNotificationsChangedMessage(QuickSetupWizardNotificationsChanged value)
    : ValueChangedMessage<QuickSetupWizardNotificationsChanged>(value);


