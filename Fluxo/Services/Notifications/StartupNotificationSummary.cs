using Fluxo.ViewModels.Shell.Main;

namespace Fluxo.Services.Notifications;

public sealed record StartupNotificationSummary(
    int GroupCount,
    NotificationGroupCategory PrimaryGroupCategory,
    int PrimaryGroupItemCount,
    string PrimaryHeader,
    string PrimaryEntityName,
    string Message);
