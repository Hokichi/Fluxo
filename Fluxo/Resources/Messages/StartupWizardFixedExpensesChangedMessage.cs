using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Fluxo.Resources.Messages;

public readonly record struct StartupWizardFixedExpensesChanged(
    int Count,
    decimal TotalAmount);

public sealed class StartupWizardFixedExpensesChangedMessage(StartupWizardFixedExpensesChanged value)
    : ValueChangedMessage<StartupWizardFixedExpensesChanged>(value);

