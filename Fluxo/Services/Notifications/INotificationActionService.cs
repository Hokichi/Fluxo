using Fluxo.ViewModels.Popups;
using Fluxo.ViewModels.Shell.Main;

namespace Fluxo.Services.Notifications;

public interface INotificationActionService
{
    Task<bool> ExecuteChecklistActionAsync(
        NotificationItemVM card,
        IReadOnlyCollection<int> selectedIds,
        CancellationToken cancellationToken = default);

    Task<bool> ExecuteGoalActionAsync(
        NotificationItemVM card,
        GoalDeadlineActionType actionType,
        CancellationToken cancellationToken = default);
}
