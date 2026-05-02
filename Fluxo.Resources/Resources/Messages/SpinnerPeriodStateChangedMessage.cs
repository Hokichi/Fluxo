namespace Fluxo.Resources.Resources.Messages;

public readonly record struct SpinnerPeriodState(
    bool IsAtCurrentPeriod,
    bool IsSpinnerVisible,
    string MoveToCurrentLabel);

public sealed class SpinnerPeriodStateChangedMessage(SpinnerPeriodState value)
    : ValueChangedMessage<SpinnerPeriodState>(value);

