using Fluxo.Core.Enums;

namespace Fluxo.Resources.Resources.Messages;

public sealed record FloatingNotificationRequest(
    string Header,
    string Message,
    IReadOnlyList<string> Details,
    NotificationSeverity Severity,
    Func<Task>? ClickAsync = null,
    Guid Id = default);

public sealed class ShowFloatingNotificationMessage(FloatingNotificationRequest value)
    : ValueChangedMessage<FloatingNotificationRequest>(value);

public sealed class DismissFloatingNotificationMessage(Guid value)
    : ValueChangedMessage<Guid>(value);
