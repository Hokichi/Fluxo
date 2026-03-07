using Fluxo.Core.Entities;

namespace Fluxo.Core.Interfaces.Services;

public interface INotificationService
{
    /// <summary>
    /// Called on app startup. Queries fixed expenses due within the
    /// configured NotificationLeadDays window and fires a Windows toast
    /// for each one that hasn't been paid yet.
    /// </summary>
    Task CheckAndNotifyDueExpensesAsync();

    /// <summary>Sends a single toast for a specific fixed expense (e.g. after manual trigger).</summary>
    Task NotifyExpenseDueAsync(FixedExpense expense, int daysUntilDue);

    /// <summary>
    /// Fires a test notification so the user can verify OS notification
    /// permissions are granted before relying on them.
    /// </summary>
    Task SendTestNotificationAsync();

    /// <summary>
    /// Returns false when OS notification permission is not granted,
    /// so the UI can show a "Enable notifications" prompt.
    /// </summary>
    bool IsSupported { get; }
}