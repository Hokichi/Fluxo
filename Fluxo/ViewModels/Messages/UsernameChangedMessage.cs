using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Fluxo.ViewModels.Messages;

public sealed class UsernameChangedMessage(string value) : ValueChangedMessage<string>(value);
