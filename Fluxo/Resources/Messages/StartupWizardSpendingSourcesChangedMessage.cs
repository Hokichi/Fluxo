using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Fluxo.Resources.Messages;

public readonly record struct StartupWizardSpendingSourcesChanged(
    int Count,
    bool HasAny,
    decimal TotalPrimaryAmount);

public sealed class StartupWizardSpendingSourcesChangedMessage(StartupWizardSpendingSourcesChanged value)
    : ValueChangedMessage<StartupWizardSpendingSourcesChanged>(value);

