using System.Windows;

namespace Fluxo.Services.Updates;

public interface IAppUpdateInteractionService
{
    Task HandleAvailableUpdateAsync(AppUpdateCheckResult update, Window? owner);
}
