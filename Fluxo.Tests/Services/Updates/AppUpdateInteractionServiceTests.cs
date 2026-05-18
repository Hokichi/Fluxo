using System.Windows;
using Fluxo.Services.Dialogs;
using Fluxo.Services.Updates;
using NSubstitute;
using Xunit;

namespace Fluxo.Tests.Services.Updates;

public sealed class AppUpdateInteractionServiceTests
{
    [Fact]
    public async Task HandleAvailableUpdateAsync_WhenFirstPromptDeclined_DoesNotDownloadLaunchOrDelete()
    {
        var update = CreateAvailableUpdate();
        var dialogService = Substitute.For<IDialogService>();
        dialogService.ShowQuestion(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Window?>(), Arg.Any<MessageBoxButton>())
            .Returns(MessageBoxResult.No);
        ConfigureToastToRunWork(dialogService);

        var appUpdateService = Substitute.For<IAppUpdateService>();
        var lifecycleService = Substitute.For<IAppUpdateLifecycleService>();
        var sut = new AppUpdateInteractionService(dialogService, appUpdateService, lifecycleService);

        await sut.HandleAvailableUpdateAsync(update, owner: null);

        await appUpdateService
            .DidNotReceive()
            .DownloadInstallerAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        appUpdateService.DidNotReceive().DeleteInstaller(Arg.Any<string>());
        lifecycleService.DidNotReceive().LaunchUpdateInstallerAndShutdown(Arg.Any<string>());
    }

    [Fact]
    public async Task HandleAvailableUpdateAsync_WhenInstallPromptDeclined_DeletesDownloadedInstaller()
    {
        const string installerPath = "C:\\temp\\fluxo-installer.exe";
        var update = CreateAvailableUpdate();
        var dialogService = Substitute.For<IDialogService>();
        dialogService.ShowQuestion(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Window?>(), Arg.Any<MessageBoxButton>())
            .Returns(MessageBoxResult.Yes, MessageBoxResult.No);
        ConfigureToastToRunWork(dialogService);

        var appUpdateService = Substitute.For<IAppUpdateService>();
        appUpdateService
            .DownloadInstallerAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(installerPath);
        var lifecycleService = Substitute.For<IAppUpdateLifecycleService>();
        var sut = new AppUpdateInteractionService(dialogService, appUpdateService, lifecycleService);

        await sut.HandleAvailableUpdateAsync(update, owner: null);

        await appUpdateService
            .Received(1)
            .DownloadInstallerAsync(update.InstallerDownloadUrl!, update.InstallerAssetName!, Arg.Any<CancellationToken>());
        appUpdateService.Received(1).DeleteInstaller(installerPath);
        lifecycleService.DidNotReceive().LaunchUpdateInstallerAndShutdown(Arg.Any<string>());
    }

    [Fact]
    public async Task HandleAvailableUpdateAsync_WhenDownloadFails_ShowsErrorAndDoesNotLaunch()
    {
        var update = CreateAvailableUpdate();
        var dialogService = Substitute.For<IDialogService>();
        dialogService.ShowQuestion(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Window?>(), Arg.Any<MessageBoxButton>())
            .Returns(MessageBoxResult.Yes);
        ConfigureToastToRunWork(dialogService);

        var appUpdateService = Substitute.For<IAppUpdateService>();
        appUpdateService
            .DownloadInstallerAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string>(new InvalidOperationException("download failed")));
        var lifecycleService = Substitute.For<IAppUpdateLifecycleService>();
        var sut = new AppUpdateInteractionService(dialogService, appUpdateService, lifecycleService);

        await sut.HandleAvailableUpdateAsync(update, owner: null);

        dialogService.Received(1).ShowError(
            Arg.Any<string>(),
            "Check for Updates",
            null,
            MessageBoxButton.OK);
        lifecycleService.DidNotReceive().LaunchUpdateInstallerAndShutdown(Arg.Any<string>());
    }

    [Fact]
    public async Task HandleAvailableUpdateAsync_WhenLaunchFails_ShowsError()
    {
        const string installerPath = "C:\\temp\\fluxo-installer.exe";
        var update = CreateAvailableUpdate();
        var dialogService = Substitute.For<IDialogService>();
        dialogService.ShowQuestion(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Window?>(), Arg.Any<MessageBoxButton>())
            .Returns(MessageBoxResult.Yes, MessageBoxResult.Yes);
        ConfigureToastToRunWork(dialogService);

        var appUpdateService = Substitute.For<IAppUpdateService>();
        appUpdateService
            .DownloadInstallerAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(installerPath);
        var lifecycleService = Substitute.For<IAppUpdateLifecycleService>();
        lifecycleService
            .When(service => service.LaunchUpdateInstallerAndShutdown(installerPath))
            .Do(_ => throw new InvalidOperationException("launch failed"));
        var sut = new AppUpdateInteractionService(dialogService, appUpdateService, lifecycleService);

        await sut.HandleAvailableUpdateAsync(update, owner: null);

        dialogService.Received(1).ShowError(
            Arg.Any<string>(),
            "Install Update",
            null,
            MessageBoxButton.OK);
    }

    [Fact]
    public async Task HandleAvailableUpdateAsync_WhenBothPromptsAccepted_LaunchesInstallerOnce_AndDoesNotDeleteInstaller()
    {
        const string installerPath = "C:\\temp\\fluxo-installer.exe";
        var update = CreateAvailableUpdate();
        var dialogService = Substitute.For<IDialogService>();
        dialogService.ShowQuestion(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Window?>(), Arg.Any<MessageBoxButton>())
            .Returns(MessageBoxResult.Yes, MessageBoxResult.Yes);
        ConfigureToastToRunWork(dialogService);

        var appUpdateService = Substitute.For<IAppUpdateService>();
        appUpdateService
            .DownloadInstallerAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(installerPath);
        var lifecycleService = Substitute.For<IAppUpdateLifecycleService>();
        var sut = new AppUpdateInteractionService(dialogService, appUpdateService, lifecycleService);

        await sut.HandleAvailableUpdateAsync(update, owner: null);

        lifecycleService.Received(1).LaunchUpdateInstallerAndShutdown(installerPath);
        appUpdateService.DidNotReceive().DeleteInstaller(Arg.Any<string>());
    }

    [Fact]
    public void BuildAvailableUpdatePrompt_UsesLatestVersion()
    {
        var update = AppUpdateCheckResult.UpdateAvailable(
            "2.5.0",
            "fluxo-2.5.0-Installer.exe",
            "https://example.test/fluxo-2.5.0-Installer.exe");

        var prompt = AppUpdateInteractionService.BuildAvailableUpdatePrompt(update);

        Assert.Equal("Fluxo 2.5.0 is available. Download and install it?", prompt);
    }

    [Fact]
    public void BuildAvailableUpdatePrompt_FallsBackToUnknown_WhenLatestVersionMissing()
    {
        var update = AppUpdateCheckResult.UpdateAvailable(
            " ",
            "fluxo-2.5.0-Installer.exe",
            "https://example.test/fluxo-2.5.0-Installer.exe");

        var prompt = AppUpdateInteractionService.BuildAvailableUpdatePrompt(update);

        Assert.Equal("Fluxo Unknown is available. Download and install it?", prompt);
    }

    private static void ConfigureToastToRunWork(IDialogService dialogService)
    {
        dialogService
            .ShowToastWhileAsync(
                Arg.Any<string>(),
                Arg.Any<Func<Task>>(),
                Arg.Any<Window?>())
            .Returns(callInfo => callInfo.ArgAt<Func<Task>>(1)());
    }

    private static AppUpdateCheckResult CreateAvailableUpdate()
    {
        return AppUpdateCheckResult.UpdateAvailable(
            "2.5.0",
            "fluxo-2.5.0-Installer.exe",
            "https://example.test/fluxo-2.5.0-Installer.exe");
    }
}
