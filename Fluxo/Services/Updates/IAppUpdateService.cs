namespace Fluxo.Services.Updates;

public interface IAppUpdateService
{
    Task<AppUpdateCheckResult> CheckForUpdatesAsync(
        string currentVersion,
        CancellationToken cancellationToken = default);

    Task<string> DownloadInstallerAsync(
        string installerDownloadUrl,
        string installerAssetName,
        CancellationToken cancellationToken = default);

    Task<string> DownloadInstallerAsync(
        string installerDownloadUrl,
        string installerAssetName,
        IProgress<double>? progress,
        CancellationToken cancellationToken = default);

    void DeleteInstaller(string installerPath);
}
