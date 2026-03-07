using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces.Services;
using Microsoft.Toolkit.Uwp.Notifications;

namespace Fluxo.Services.Persistence;

/// <summary>
///     Windows toast notification service.
///     Requires the Microsoft.Toolkit.Uwp.Notifications NuGet package.
///     The app does NOT need to be packaged (MSIX) to show toasts on Windows 10/11
///     when registered as a COM server — the Toolkit handles this automatically.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private readonly IFixedExpenseService _fixedExpenseService;
    private readonly IAppSettingService _settings;

    public NotificationService(IFixedExpenseService fixedExpenseService, IAppSettingService settings)
    {
        _fixedExpenseService = fixedExpenseService;
        _settings = settings;
    }

    public bool IsSupported => Environment.OSVersion.Platform == PlatformID.Win32NT
                               && Environment.OSVersion.Version.Major >= 10;

    /// <summary>
    ///     Called once on app startup. Reads the configured lead-time window
    ///     (default 3 days) and fires a toast for each unpaid fixed expense
    ///     that falls within that window.
    /// </summary>
    public async Task CheckAndNotifyDueExpensesAsync()
    {
        if (!IsSupported) return;

        var leadDays = await _settings.GetNotificationLeadDaysAsync();
        var dueSoon = await _fixedExpenseService.GetDueSoonAsync(leadDays);

        foreach (var due in dueSoon)
        {
            var fe = new FixedExpense { Id = due.FixedExpenseId, Name = due.Name };
            await NotifyExpenseDueAsync(fe, due.DaysUntilDue);
        }
    }

    public Task NotifyExpenseDueAsync(FixedExpense expense, int daysUntilDue)
    {
        if (!IsSupported) return Task.CompletedTask;

        var dueText = daysUntilDue switch
        {
            < 0 => $"overdue by {Math.Abs(daysUntilDue)} day(s)",
            0 => "due today",
            1 => "due tomorrow",
            _ => $"due in {daysUntilDue} days"
        };

        new ToastContentBuilder()
            .AddAppLogoOverride(new Uri("ms-appx:///Assets/FluxoIcon.png"), ToastGenericAppLogoCrop.Circle)
            .AddText($"💸 {expense.Name} is {dueText}")
            .AddText("Tap to open Fluxo and mark it as paid.")
            .AddArgument("action", "openFixedExpense")
            .AddArgument("id", expense.Id.ToString());

        return Task.CompletedTask;
    }

    public Task SendTestNotificationAsync()
    {
        if (!IsSupported) return Task.CompletedTask;

        new ToastContentBuilder()
            .AddText("🎉 Fluxo notifications are working!")
            .AddText("You'll be reminded before your bills are due.");
        return Task.CompletedTask;
    }
}