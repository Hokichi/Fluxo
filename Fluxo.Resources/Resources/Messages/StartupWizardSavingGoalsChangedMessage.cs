namespace Fluxo.Resources.Messages;

public readonly record struct QuickSetupWizardSavingGoalsChanged(int Count);

public sealed class QuickSetupWizardSavingGoalsChangedMessage(QuickSetupWizardSavingGoalsChanged value)
    : ValueChangedMessage<QuickSetupWizardSavingGoalsChanged>(value);


