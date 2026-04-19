using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Fluxo.Resources.Messages;

public sealed class DateRangeSelectionChangedMessage(DateTime from, DateTime to)
    : ValueChangedMessage<(DateTime From, DateTime To)>((from.Date, to.Date));
