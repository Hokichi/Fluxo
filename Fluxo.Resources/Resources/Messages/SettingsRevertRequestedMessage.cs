namespace Fluxo.Resources.Resources.Messages;

public sealed class SettingsRevertRequestedMessage(SettingsOperationCorrelation value)
    : ValueChangedMessage<SettingsOperationCorrelation>(value);
