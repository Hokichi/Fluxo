namespace Fluxo.Resources.Resources.Messages;

public sealed class RecordLogMemoryMessage(ILogMemoryAction value)
    : ValueChangedMessage<ILogMemoryAction>(value);