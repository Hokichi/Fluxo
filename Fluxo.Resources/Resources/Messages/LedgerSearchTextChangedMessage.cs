using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Fluxo.Resources.Resources.Messages;

public sealed class LedgerSearchTextChangedMessage(string value) : ValueChangedMessage<string>(value);
