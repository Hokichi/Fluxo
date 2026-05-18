using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.Services.Updates;
using NSubstitute;
using System.Text;
using Xunit;

namespace Fluxo.Tests.Services.Updates;

public sealed class StartupUpdateNotificationServiceTests
{
    [Fact]
    public async Task CheckAndSyncAsync_DoesNotRunDataSync_WhenUpdateCheckReturnsError()
    {
        var appUpdateService = Substitute.For<IAppUpdateService>();
        appUpdateService
            .CheckForUpdatesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(AppUpdateCheckResult.Error("network unavailable"));
        var dataOperationRunner = Substitute.For<IDataOperationRunner>();

        var sut = new StartupUpdateNotificationService(appUpdateService, dataOperationRunner);

        await sut.CheckAndSyncAsync();

        await dataOperationRunner
            .DidNotReceive()
            .RunAsync(
                Arg.Any<string>(),
                Arg.Any<Func<IDataOperationScope, CancellationToken, Task>>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckAndSyncAsync_SuppressesNonCancellationExceptions_FromDataSync()
    {
        var appUpdateService = Substitute.For<IAppUpdateService>();
        appUpdateService
            .CheckForUpdatesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(AppUpdateCheckResult.UpToDate("1.2.3"));
        var dataOperationRunner = Substitute.For<IDataOperationRunner>();
        dataOperationRunner
            .RunAsync(
                Arg.Any<string>(),
                Arg.Any<Func<IDataOperationScope, CancellationToken, Task>>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => throw new InvalidOperationException("db sync failed"));

        var sut = new StartupUpdateNotificationService(appUpdateService, dataOperationRunner);

        var exception = await Record.ExceptionAsync(() => sut.CheckAndSyncAsync());

        Assert.Null(exception);
        await dataOperationRunner
            .Received(1)
            .RunAsync(
                Arg.Any<string>(),
                Arg.Any<Func<IDataOperationScope, CancellationToken, Task>>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckAndSyncAsync_PropagatesCancellation_FromDataSync()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var appUpdateService = Substitute.For<IAppUpdateService>();
        appUpdateService
            .CheckForUpdatesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(AppUpdateCheckResult.UpToDate("1.2.3"));
        var dataOperationRunner = Substitute.For<IDataOperationRunner>();
        dataOperationRunner
            .RunAsync(
                Arg.Any<string>(),
                Arg.Any<Func<IDataOperationScope, CancellationToken, Task>>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromCanceled(callInfo.ArgAt<CancellationToken>(2)));

        var sut = new StartupUpdateNotificationService(appUpdateService, dataOperationRunner);

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            sut.CheckAndSyncAsync(cancellationTokenSource.Token));

        Assert.Equal(cancellationTokenSource.Token, exception.CancellationToken);
    }

    [Fact]
    public async Task CheckAndSyncAsync_PropagatesCancellation_FromUpdateCheck()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var appUpdateService = Substitute.For<IAppUpdateService>();
        appUpdateService
            .CheckForUpdatesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
                Task.FromCanceled<AppUpdateCheckResult>(callInfo.ArgAt<CancellationToken>(1)));
        var dataOperationRunner = Substitute.For<IDataOperationRunner>();

        var sut = new StartupUpdateNotificationService(appUpdateService, dataOperationRunner);

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            sut.CheckAndSyncAsync(cancellationTokenSource.Token));

        Assert.Equal(cancellationTokenSource.Token, exception.CancellationToken);
    }

    [Fact]
    public void BuildNotificationForUpdate_CreatesExpectedNotification()
    {
        var createdOn = new DateTime(2026, 05, 18, 10, 30, 0, DateTimeKind.Utc);
        var update = AppUpdateCheckResult.UpdateAvailable(
            "1.2.3",
            "fluxo-1.2.3-Installer.exe",
            "https://example.test/fluxo-1.2.3-Installer.exe");

        var notification = StartupUpdateNotificationService.BuildNotificationForUpdate(update, createdOn);

        Assert.Equal(
            $"AppUpdate-{EncodeToken("1.2.3")}.{EncodeToken("fluxo-1.2.3-Installer.exe")}.{EncodeToken("https://example.test/fluxo-1.2.3-Installer.exe")}",
            notification.Type);
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

    [Fact]
    public void BuildNotificationForUpdate_Throws_WhenLatestVersionMissing()
    {
        var update = AppUpdateCheckResult.UpdateAvailable(
            " ",
            "fluxo-1.2.3-Installer.exe",
            "https://example.test/fluxo-1.2.3-Installer.exe");

        Assert.Throws<ArgumentException>(() =>
            StartupUpdateNotificationService.BuildNotificationForUpdate(update, DateTime.UtcNow));
    }

    [Fact]
    public void BuildNotificationForUpdate_Throws_WhenInstallerAssetMissing()
    {
        var update = AppUpdateCheckResult.UpdateAvailable(
            "1.2.3",
            " ",
            "https://example.test/fluxo-1.2.3-Installer.exe");

        Assert.Throws<ArgumentException>(() =>
            StartupUpdateNotificationService.BuildNotificationForUpdate(update, DateTime.UtcNow));
    }

    [Fact]
    public void BuildNotificationForUpdate_Throws_WhenInstallerUrlMissing()
    {
        var update = AppUpdateCheckResult.UpdateAvailable(
            "1.2.3",
            "fluxo-1.2.3-Installer.exe",
            " ");

        Assert.Throws<ArgumentException>(() =>
            StartupUpdateNotificationService.BuildNotificationForUpdate(update, DateTime.UtcNow));
    }

    private static string EncodeToken(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
