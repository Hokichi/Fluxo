using System.Windows;
using Fluxo.Services.Dialogs;
using Fluxo.Services.Logging;

namespace Fluxo.Services.Updates;

public sealed class AppUpdateInteractionService(
    IDialogService dialogService,
    IAppUpdateService appUpdateService)
    : IAppUpdateInteractionService
{
    public async Task HandleAvailableUpdateAsync(AppUpdateCheckResult update, Window? owner)
    {
        ArgumentNullException.ThrowIfNull(update);

        if (update.Status != AppUpdateCheckStatus.UpdateAvailable)
            return;

        var prompt = BuildAvailableUpdatePrompt(update);
        if (dialogService.ShowQuestion(prompt, "Update Available", owner) != MessageBoxResult.Yes)
            return;

        string installerPath;
        try
        {
            installerPath = await RunWithToastAsync(
                "Downloading update",
                () => DownloadInstallerAsync(update),
                owner);
        }
        catch (Exception exception)
        {
            FluxoLogManager.LogFailureForProcess(exception, "download Fluxo update");
            dialogService.ShowError(
                FluxoLogManager.CreateFailureMessage("download Fluxo update"),
                "Check for Updates",
                owner);
            return;
        }

        if (dialogService.ShowQuestion(
                "The update installer has been downloaded. Close Fluxo and start the installer now?",
                "Install Update",
                owner) != MessageBoxResult.Yes)
        {
            appUpdateService.DeleteInstaller(installerPath);
            return;
        }

        try
        {
            if (Application.Current is not App app)
                throw new InvalidOperationException("The Fluxo application instance is unavailable.");

            app.LaunchUpdateInstallerAndShutdown(installerPath);
        }
        catch (Exception exception)
        {
            FluxoLogManager.LogFailureForProcess(exception, "start Fluxo update installer");
            dialogService.ShowError(
                FluxoLogManager.CreateFailureMessage("start Fluxo update installer"),
                "Install Update",
                owner);
        }
    }

    internal static string BuildAvailableUpdatePrompt(AppUpdateCheckResult update)
    {
        ArgumentNullException.ThrowIfNull(update);

        var version = string.IsNullOrWhiteSpace(update.LatestVersion)
            ? "Unknown"
            : update.LatestVersion.Trim();
        return $"Fluxo {version} is available. Download and install it?";
    }

    private async Task<string> DownloadInstallerAsync(AppUpdateCheckResult update)
    {
        if (string.IsNullOrWhiteSpace(update.InstallerDownloadUrl)
            || string.IsNullOrWhiteSpace(update.InstallerAssetName))
        {
            throw new InvalidOperationException("No Fluxo update installer is available to download.");
        }

        return await appUpdateService.DownloadInstallerAsync(
            update.InstallerDownloadUrl,
            update.InstallerAssetName);
    }

    private async Task<T> RunWithToastAsync<T>(string message, Func<Task<T>> work, Window? owner)
    {
        T? result = default;
        await dialogService.ShowToastWhileAsync(message, async () =>
        {
            result = await work();
        }, owner);

        return result!;
    }
}
