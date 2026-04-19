using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Fluxo.Resources.Messages;

public sealed class SettingsRevertRequestedMessage(SettingsOperationCorrelation value)
    : ValueChangedMessage<SettingsOperationCorrelation>(value);
