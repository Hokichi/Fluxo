namespace Fluxo.Resources.Resources.Messages;

public sealed class DateRangeSelectionChangedMessage(DateTime from, DateTime to)
    : ValueChangedMessage<(DateTime From, DateTime To)>((from.Date, to.Date));

