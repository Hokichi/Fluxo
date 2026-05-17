namespace Fluxo.Resources.Resources.Messages;

public sealed class UsernameChangedMessage(string value) : ValueChangedMessage<string>(value);

