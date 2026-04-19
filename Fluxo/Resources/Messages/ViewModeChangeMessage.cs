using CommunityToolkit.Mvvm.Messaging.Messages;
using Fluxo.ViewModels.Shell;

namespace Fluxo.Resources.Messages;

public sealed class ViewModeChangeMessage(MainContentViewMode value)
    : ValueChangedMessage<MainContentViewMode>(value);
