namespace Fluxo.Resources.Resources.Messages;

public sealed class SettingsApplyRequestedMessage(SettingsOperationCorrelation value)
    : ValueChangedMessage<SettingsOperationCorrelation>(value);

