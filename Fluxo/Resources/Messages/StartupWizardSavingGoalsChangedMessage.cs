using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Fluxo.Resources.Messages;

public readonly record struct StartupWizardSavingGoalsChanged(int Count);

public sealed class StartupWizardSavingGoalsChangedMessage(StartupWizardSavingGoalsChanged value)
    : ValueChangedMessage<StartupWizardSavingGoalsChanged>(value);

