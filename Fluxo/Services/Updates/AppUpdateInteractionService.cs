using System.Windows;
using Fluxo.Services.Dialogs;
using Fluxo.Services.Logging;

namespace Fluxo.Services.Updates;

public sealed class AppUpdateInteractionService(
    IDialogService dialogService,
    IAppUpdateService appUpdateService,
    IAppUpdateLifecycleService appUpdateLifecycleService)
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

        var downloadCandidate = await ResolveDownloadCandidateAsync(update);

        string installerPath;
        try
        {
            var downloadedInstallerPath = await dialogService.ShowDownloadUpdateAsync(
                downloadCandidate,
                (progress, cancellationToken) => DownloadInstallerAsync(downloadCandidate, progress, cancellationToken),
                owner);

            if (string.IsNullOrWhiteSpace(downloadedInstallerPath))
                return;

            installerPath = downloadedInstallerPath;
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
            appUpdateLifecycleService.LaunchUpdateInstallerAndShutdown(installerPath);
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

    private async Task<AppUpdateCheckResult> ResolveDownloadCandidateAsync(AppUpdateCheckResult update)
    {
        if (HasInstallerMetadata(update))
            return update;

        try
        {
            var hydratedUpdate = await appUpdateService.CheckForUpdatesAsync(AppVersionResolver.ResolveCurrentVersion());
            if (hydratedUpdate.Status != AppUpdateCheckStatus.UpdateAvailable || !HasInstallerMetadata(hydratedUpdate))
                return update;

            var version = string.IsNullOrWhiteSpace(update.LatestVersion)
                ? hydratedUpdate.LatestVersion ?? string.Empty
                : update.LatestVersion.Trim();

            return AppUpdateCheckResult.UpdateAvailable(
                version,
                hydratedUpdate.InstallerAssetName!.Trim(),
                hydratedUpdate.InstallerDownloadUrl!.Trim());
        }
        catch (Exception exception)
        {
            FluxoLogManager.LogWarning(exception, "Unable to hydrate Fluxo update metadata before download.");
            return update;
        }
    }

    private static bool HasInstallerMetadata(AppUpdateCheckResult update)
    {
        return !string.IsNullOrWhiteSpace(update.InstallerAssetName)
            && !string.IsNullOrWhiteSpace(update.InstallerDownloadUrl)
            && Uri.TryCreate(update.InstallerDownloadUrl.Trim(), UriKind.Absolute, out _);
    }

    private async Task<string> DownloadInstallerAsync(
        AppUpdateCheckResult update,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(update.InstallerDownloadUrl)
            || string.IsNullOrWhiteSpace(update.InstallerAssetName))
        {
            throw new InvalidOperationException("No Fluxo update installer is available to download.");
        }

        return await appUpdateService.DownloadInstallerAsync(
            update.InstallerDownloadUrl,
            update.InstallerAssetName,
            progress,
            cancellationToken);
    }
}
