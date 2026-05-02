namespace Fluxo.Resources.Messages;

public sealed class SettingsApplyRequestedMessage(SettingsOperationCorrelation value)
    : ValueChangedMessage<SettingsOperationCorrelation>(value);

