using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Fluxo.Resources.Messages;

public sealed class UsernameChangedMessage(string value) : ValueChangedMessage<string>(value);
