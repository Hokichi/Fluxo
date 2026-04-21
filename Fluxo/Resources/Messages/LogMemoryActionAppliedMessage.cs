using CommunityToolkit.Mvvm.Messaging.Messages;
using Fluxo.Services.History;

namespace Fluxo.Resources.Messages;

public enum LogMemoryApplyDirection
{
    Redo,
    Undo
}

public sealed class LogMemoryActionAppliedMessage(ILogMemoryAction action, LogMemoryApplyDirection direction)
    : ValueChangedMessage<(ILogMemoryAction Action, LogMemoryApplyDirection Direction)>((action, direction));
