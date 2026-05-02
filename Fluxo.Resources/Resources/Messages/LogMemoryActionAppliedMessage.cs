namespace Fluxo.Resources.Resources.Messages;

public enum LogMemoryApplyDirection
{
    Redo,
    Undo
}

public sealed class LogMemoryActionAppliedMessage(ILogMemoryAction action, LogMemoryApplyDirection direction)
    : ValueChangedMessage<(ILogMemoryAction Action, LogMemoryApplyDirection Direction)>((action, direction));
