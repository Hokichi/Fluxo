namespace Fluxo.Resources.Messages;

public sealed class ViewModeChangeMessage(MainContentViewMode value)
    : ValueChangedMessage<MainContentViewMode>(value);

