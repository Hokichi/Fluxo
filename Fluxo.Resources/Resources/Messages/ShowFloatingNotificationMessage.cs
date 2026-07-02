using Fluxo.Core.Enums;

namespace Fluxo.Resources.Resources.Messages;

public sealed record FloatingNotificationRequest(
    string Header,
    string Message,
    IReadOnlyList<string> Details,
    NotificationSeverity Severity,
    Func<Task>? ClickAsync = null);

public sealed class ShowFloatingNotificationMessage(FloatingNotificationRequest value)
    : ValueChangedMessage<FloatingNotificationRequest>(value);
