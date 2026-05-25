using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace Fluxo.Installer.Services;

public enum DotNetRuntimeInstallStatus
{
    AlreadyInstalled,
    InstalledByFluxo,
    Failed,
    Cancelled,
}

public enum DotNetRuntimeUninstallStatus
{
    Skipped,
    Uninstalled,
    Failed,
}

public sealed record DotNetRuntimeInstallResult(DotNetRuntimeInstallStatus Status, string Message);

public sealed record DotNetRuntimeUninstallResult(DotNetRuntimeUninstallStatus Status, string Message);

public interface IDotNetRuntimeInstaller
{
    Task<DotNetRuntimeInstallResult> EnsureInstalledAsync(CancellationToken cancellationToken);
    Task RollbackRuntimeInstalledByFluxoAsync(CancellationToken cancellationToken);
    Task<DotNetRuntimeUninstallResult> UninstallOwnedRuntimeAsync(CancellationToken cancellationToken);
    void RequestCancellation();
    void CleanupDownloadedInstaller();
}

public sealed class DotNetRuntimeInstaller : IDotNetRuntimeInstaller
{
    private const string ReleasesIndexUrl = "https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/releases-index.json";
    private const string InstallArguments = "/install /quiet /norestart";
    private const string UninstallArguments = "/uninstall /quiet /norestart";

    private readonly IDotNetRuntimeDetector runtimeDetector;
    private readonly IDotNetRuntimeOwnershipStore ownershipStore;
    private readonly Func<string, string, CancellationToken, Task> downloadFileAsync;
    private readonly Func<string, string, CancellationToken, Task<int>> runProcessAsync;
    private readonly Action<string> deleteFile;
    private readonly Func<string, string> tempPathFactory;
    private readonly Func<CancellationToken, Task<DotNetRuntimeInstallerInfo>> releaseInfoProvider;
    private string? downloadedInstallerPath;

    public DotNetRuntimeInstaller(
        IDotNetRuntimeDetector? runtimeDetector = null,
        IDotNetRuntimeOwnershipStore? ownershipStore = null,
        Func<string, string, CancellationToken, Task>? downloadFileAsync = null,
        Func<string, string, CancellationToken, Task<int>>? runProcessAsync = null,
        Action<string>? deleteFile = null,
        Func<string, string>? tempPathFactory = null,
        Func<CancellationToken, Task<DotNetRuntimeInstallerInfo>>? releaseInfoProvider = null)
    {
        this.runtimeDetector = runtimeDetector ?? new DotNetRuntimeDetector();
        this.ownershipStore = ownershipStore ?? new DotNetRuntimeOwnershipStore();
        this.downloadFileAsync = downloadFileAsync ?? DownloadFileAsync;
        this.runProcessAsync = runProcessAsync ?? RunProcessAsync;
        this.deleteFile = deleteFile ?? File.Delete;
        this.tempPathFactory = tempPathFactory ?? (fileName => Path.Combine(Path.GetTempPath(), fileName));
        this.releaseInfoProvider = releaseInfoProvider ?? ResolveLatestRuntimeInstallerAsync;
    }

    public async Task<DotNetRuntimeInstallResult> EnsureInstalledAsync(CancellationToken cancellationToken)
    {
        if (runtimeDetector.IsRequiredRuntimeInstalled())
        {
            return new DotNetRuntimeInstallResult(
                DotNetRuntimeInstallStatus.AlreadyInstalled,
                "Windows Desktop Runtime is already installed.");
        }

        try
        {
            var info = await releaseInfoProvider(cancellationToken).ConfigureAwait(false);
            downloadedInstallerPath = tempPathFactory(info.FileName);
            await downloadFileAsync(info.Url, downloadedInstallerPath, cancellationToken).ConfigureAwait(false);

            var exitCode = await runProcessAsync(downloadedInstallerPath, InstallArguments, cancellationToken)
                .ConfigureAwait(false);
            if (exitCode != 0)
            {
                CleanupDownloadedInstaller();
                return new DotNetRuntimeInstallResult(
                    DotNetRuntimeInstallStatus.Failed,
                    $"Runtime installer exited with code {exitCode}.");
            }

            if (!runtimeDetector.IsRequiredRuntimeInstalled())
            {
                CleanupDownloadedInstaller();
                return new DotNetRuntimeInstallResult(
                    DotNetRuntimeInstallStatus.Failed,
                    "Windows Desktop Runtime could not be verified after installation.");
            }

            ownershipStore.Save(new DotNetRuntimeOwnershipMarker(
                info.Version,
                info.Rid,
                info.Url,
                InstalledByFluxo: true));
            CleanupDownloadedInstaller();
            return new DotNetRuntimeInstallResult(
                DotNetRuntimeInstallStatus.InstalledByFluxo,
                "Windows Desktop Runtime installed.");
        }
        catch (OperationCanceledException)
        {
            CleanupDownloadedInstaller();
            return new DotNetRuntimeInstallResult(
                DotNetRuntimeInstallStatus.Cancelled,
                "Runtime installation was cancelled.");
        }
        catch (Exception ex)
        {
            CleanupDownloadedInstaller();
            return new DotNetRuntimeInstallResult(DotNetRuntimeInstallStatus.Failed, ex.Message);
        }
    }

    public async Task RollbackRuntimeInstalledByFluxoAsync(CancellationToken cancellationToken)
    {
        var marker = ownershipStore.Read();
        if (marker is null)
        {
            CleanupDownloadedInstaller();
            return;
        }

        var exitCode = await RunRuntimeUninstallAsync(marker, cancellationToken).ConfigureAwait(false);
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Runtime uninstaller exited with code {exitCode}.");
        }

        ownershipStore.Clear();
        CleanupDownloadedInstaller();
    }

    public async Task<DotNetRuntimeUninstallResult> UninstallOwnedRuntimeAsync(CancellationToken cancellationToken)
    {
        var marker = ownershipStore.Read();
        if (marker is null)
        {
            return new DotNetRuntimeUninstallResult(
                DotNetRuntimeUninstallStatus.Skipped,
                "Runtime was not installed by Fluxo.");
        }

        try
        {
            var exitCode = await RunRuntimeUninstallAsync(marker, cancellationToken).ConfigureAwait(false);
            if (exitCode != 0)
            {
                CleanupDownloadedInstaller();
                return new DotNetRuntimeUninstallResult(
                    DotNetRuntimeUninstallStatus.Failed,
                    $"Runtime uninstaller exited with code {exitCode}.");
            }

            ownershipStore.Clear();
            CleanupDownloadedInstaller();
            return new DotNetRuntimeUninstallResult(
                DotNetRuntimeUninstallStatus.Uninstalled,
                "Runtime installed by Fluxo was removed.");
        }
        catch (Exception ex)
        {
            CleanupDownloadedInstaller();
            return new DotNetRuntimeUninstallResult(DotNetRuntimeUninstallStatus.Failed, ex.Message);
        }
    }

    public void RequestCancellation()
    {
        // Cancellation is controlled through caller-provided tokens. Do not kill Windows Installer
        // mid-transaction unless a future implementation can prove that it is safe.
    }

    public void CleanupDownloadedInstaller()
    {
        if (string.IsNullOrWhiteSpace(downloadedInstallerPath))
        {
            return;
        }

        try
        {
            deleteFile(downloadedInstallerPath);
        }
        catch
        {
        }
    }

    private async Task<int> RunRuntimeUninstallAsync(
        DotNetRuntimeOwnershipMarker marker,
        CancellationToken cancellationToken)
    {
        var installerFileName = Path.GetFileName(new Uri(marker.InstallerUrl).AbsolutePath);
        downloadedInstallerPath = tempPathFactory(installerFileName);
        await downloadFileAsync(marker.InstallerUrl, downloadedInstallerPath, cancellationToken).ConfigureAwait(false);
        return await runProcessAsync(downloadedInstallerPath, UninstallArguments, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<DotNetRuntimeInstallerInfo> ResolveLatestRuntimeInstallerAsync(
        CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();
        var indexJson = await httpClient.GetStringAsync(ReleasesIndexUrl, cancellationToken).ConfigureAwait(false);
        var releasesUrl = DotNetRuntimeReleaseResolver.ResolveReleasesJsonUrl(indexJson);
        var releasesJson = await httpClient.GetStringAsync(releasesUrl, cancellationToken).ConfigureAwait(false);
        return DotNetRuntimeReleaseResolver.ResolveLatestWindowsDesktopRuntimeInstaller(indexJson, releasesJson);
    }

    private static async Task DownloadFileAsync(
        string url,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();
        await using var input = await httpClient.GetStreamAsync(url, cancellationToken).ConfigureAwait(false);
        await using var output = File.Create(destinationPath);
        await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> RunProcessAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            },
        };

        process.Start();
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return process.ExitCode;
    }
}
