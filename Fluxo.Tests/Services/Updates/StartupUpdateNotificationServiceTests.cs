using Fluxo.Core.Entities;
using Fluxo.Services.Updates;
using Xunit;

namespace Fluxo.Tests.Services.Updates;

public sealed class StartupUpdateNotificationServiceTests
{
    [Fact]
    public void BuildNotificationForUpdate_CreatesExpectedNotification()
    {
        var createdOn = new DateTime(2026, 05, 18, 10, 30, 0, DateTimeKind.Utc);
        var update = AppUpdateCheckResult.UpdateAvailable(
            "1.2.3",
            "fluxo-1.2.3-Installer.exe",
            "https://example.test/fluxo-1.2.3-Installer.exe");

        var notification = StartupUpdateNotificationService.BuildNotificationForUpdate(update, createdOn);

        Assert.Equal("AppUpdate-1.2.3", notification.Type);
        Assert.Equal("New Update Found", notification.Header);
        Assert.Equal("Version 1.2.3 is available for download", notification.Message);
        Assert.Equal(createdOn, notification.CreatedOn);
        Assert.False(notification.IsCleared);
        Assert.False(notification.IsForDeletion);
    }

    [Fact]
    public void IsAppUpdateNotification_ReturnsTrue_ForAppUpdatePrefix()
    {
        var notification = new Notification
        {
            Type = "AppUpdate-1.2.3"
        };

        var isUpdateNotification = StartupUpdateNotificationService.IsAppUpdateNotification(notification);

        Assert.True(isUpdateNotification);
    }

    [Fact]
    public void IsAppUpdateNotification_ReturnsFalse_ForNonUpdateType()
    {
        var notification = new Notification
        {
            Type = "GoalDeadline-3_20260503"
        };

        var isUpdateNotification = StartupUpdateNotificationService.IsAppUpdateNotification(notification);

        Assert.False(isUpdateNotification);
    }
}
