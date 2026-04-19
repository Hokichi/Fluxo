using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Fluxo.Resources.Messages;

public readonly record struct StartupWizardBudgetAllocationChanged(
    int NeedsPercentage,
    int WantsPercentage,
    int InvestPercentage,
    bool HasError);

public sealed class StartupWizardBudgetAllocationChangedMessage(StartupWizardBudgetAllocationChanged value)
    : ValueChangedMessage<StartupWizardBudgetAllocationChanged>(value);

