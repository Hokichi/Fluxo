namespace Fluxo.Resources.Messages;

public sealed class RecordLogMemoryMessage(ILogMemoryAction value)
    : ValueChangedMessage<ILogMemoryAction>(value);
