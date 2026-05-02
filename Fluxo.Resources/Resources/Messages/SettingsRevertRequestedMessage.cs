namespace Fluxo.Resources.Messages;

public sealed class SettingsRevertRequestedMessage(SettingsOperationCorrelation value)
    : ValueChangedMessage<SettingsOperationCorrelation>(value);

