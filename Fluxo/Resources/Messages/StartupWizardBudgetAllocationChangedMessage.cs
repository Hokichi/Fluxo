using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Fluxo.Resources.Messages;

public readonly record struct QuickSetupWizardBudgetAllocationChanged(
    int NeedsPercentage,
    int WantsPercentage,
    int InvestPercentage,
    bool HasError);

public sealed class QuickSetupWizardBudgetAllocationChangedMessage(QuickSetupWizardBudgetAllocationChanged value)
    : ValueChangedMessage<QuickSetupWizardBudgetAllocationChanged>(value);

