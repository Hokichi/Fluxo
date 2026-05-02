using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Shell.Main;

namespace Fluxo.Services.Notifications;

public interface INotificationGroupingService
{
    IReadOnlyList<NotificationItemVM> Group(IReadOnlyList<NotificationVM> notifications);
}
