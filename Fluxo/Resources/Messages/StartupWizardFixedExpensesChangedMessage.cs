using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Fluxo.Resources.Messages;

public readonly record struct QuickSetupWizardFixedExpensesChanged(
    int Count,
    decimal TotalAmount);

public sealed class QuickSetupWizardFixedExpensesChangedMessage(QuickSetupWizardFixedExpensesChanged value)
    : ValueChangedMessage<QuickSetupWizardFixedExpensesChanged>(value);

