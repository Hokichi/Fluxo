using Fluxo.ViewModels.Shell.Main;

namespace Fluxo.Services.Notifications;

public sealed record StartupNotificationSummary(
    string Message,
    int GroupCount,
    int NotificationCount,
    NotificationGroupCategory? Category);
