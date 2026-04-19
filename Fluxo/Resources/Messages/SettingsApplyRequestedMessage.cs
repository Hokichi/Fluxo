using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Fluxo.Resources.Messages;

public sealed class SettingsApplyRequestedMessage(SettingsOperationCorrelation value)
    : ValueChangedMessage<SettingsOperationCorrelation>(value);
