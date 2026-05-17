namespace Fluxo.Services.Updates;

public enum AppUpdateCheckStatus
{
    UpToDate,
    UpdateAvailable,
    Error
}

public sealed record AppUpdateCheckResult
{
    private AppUpdateCheckResult(
        AppUpdateCheckStatus status,
        string? latestVersion = null,
        string? installerAssetName = null,
        string? installerDownloadUrl = null,
        string? errorMessage = null)
    {
        Status = status;
        LatestVersion = latestVersion;
        InstallerAssetName = installerAssetName;
        InstallerDownloadUrl = installerDownloadUrl;
        ErrorMessage = errorMessage;
    }

    public AppUpdateCheckStatus Status { get; }

    public string? LatestVersion { get; }

    public string? InstallerAssetName { get; }

    public string? InstallerDownloadUrl { get; }

    public string? ErrorMessage { get; }

    public static AppUpdateCheckResult UpToDate(string latestVersion) =>
        new(AppUpdateCheckStatus.UpToDate, latestVersion);

    public static AppUpdateCheckResult UpdateAvailable(
        string latestVersion,
        string installerAssetName,
        string installerDownloadUrl) =>
        new(
            AppUpdateCheckStatus.UpdateAvailable,
            latestVersion,
            installerAssetName,
            installerDownloadUrl);

    public static AppUpdateCheckResult Error(string errorMessage) =>
        new(AppUpdateCheckStatus.Error, errorMessage: errorMessage);
}
