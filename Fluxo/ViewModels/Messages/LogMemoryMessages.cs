using CommunityToolkit.Mvvm.Messaging.Messages;
using Fluxo.Services.History;

namespace Fluxo.ViewModels.Messages;

public sealed class RecordLogMemoryMessage(ILogMemoryAction value)
    : ValueChangedMessage<ILogMemoryAction>(value);