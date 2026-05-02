namespace Fluxo.Resources.Messages;

public readonly record struct SettingsOperationCorrelation(Guid OperationId);

public sealed class SettingsLoadRequestedMessage(SettingsOperationCorrelation value)
    : ValueChangedMessage<SettingsOperationCorrelation>(value);

