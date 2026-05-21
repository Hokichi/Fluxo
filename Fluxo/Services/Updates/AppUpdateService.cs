using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Fluxo.Services.Updates;

public sealed partial class AppUpdateService : IAppUpdateService
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/Hokichi/Fluxo/releases/latest";
    private const string ConnectionErrorMessage =
        "Unable to check for updates. Check your internet connection and try again.";

    private readonly HttpClient _httpClient;
    private readonly Func<string> _tempDirectoryFactory;

    public AppUpdateService(HttpClient? httpClient = null, Func<string>? tempDirectoryFactory = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _tempDirectoryFactory = tempDirectoryFactory ?? Path.GetTempPath;
    }

    public async Task<AppUpdateCheckResult> CheckForUpdatesAsync(
        string currentVersion,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseVersion(currentVersion, out var localVersion))
        {
            return AppUpdateCheckResult.Error("Unable to resolve the installed Fluxo version.");
        }

        try
        {
            using var request = CreateGitHubRequest(LatestReleaseUrl);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;

            var latestVersionText = ReadString(root, "tag_name")?.TrimStart('v', 'V');
            if (!TryParseVersion(latestVersionText, out var latestVersion))
            {
                return AppUpdateCheckResult.Error("GitHub did not return a valid Fluxo release version.");
            }

            if (latestVersion <= localVersion)
            {
                return AppUpdateCheckResult.UpToDate(latestVersionText!);
            }

            if (!TrySelectInstallerAsset(root, out var assetName, out var downloadUrl))
            {
                return AppUpdateCheckResult.Error("The latest GitHub release does not include a Fluxo installer.");
            }

            return AppUpdateCheckResult.UpdateAvailable(latestVersionText!, assetName, downloadUrl);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return AppUpdateCheckResult.Error(ConnectionErrorMessage);
        }
        catch (HttpRequestException)
        {
            return AppUpdateCheckResult.Error(ConnectionErrorMessage);
        }
        catch (JsonException)
        {
            return AppUpdateCheckResult.Error("GitHub returned release data that Fluxo could not read.");
        }
    }

    public async Task<string> DownloadInstallerAsync(
        string installerDownloadUrl,
        string installerAssetName,
        CancellationToken cancellationToken = default)
        => await DownloadInstallerAsync(installerDownloadUrl, installerAssetName, null, cancellationToken);

    public async Task<string> DownloadInstallerAsync(
        string installerDownloadUrl,
        string installerAssetName,
        IProgress<double>? progress,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(installerDownloadUrl, UriKind.Absolute, out _))
        {
            throw new ArgumentException("Installer download URL must be absolute.", nameof(installerDownloadUrl));
        }

        var safeAssetName = Path.GetFileName(installerAssetName);
        if (string.IsNullOrWhiteSpace(safeAssetName)
            || !InstallerAssetNameRegex().IsMatch(safeAssetName))
        {
            throw new ArgumentException("Installer asset name is invalid.", nameof(installerAssetName));
        }

        var tempDirectory = _tempDirectoryFactory();
        Directory.CreateDirectory(tempDirectory);
        var destinationPath = Path.Combine(tempDirectory, safeAssetName);

        try
        {
            using var request = CreateGitHubRequest(installerDownloadUrl);
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var destination = new FileStream(
                destinationPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None);
            await CopyToFileWithProgressAsync(source, destination, response.Content.Headers.ContentLength, progress, cancellationToken);
        }
        catch
        {
            DeleteInstaller(destinationPath);
            throw;
        }

        return destinationPath;
    }

    public void DeleteInstaller(string installerPath)
    {
        if (string.IsNullOrWhiteSpace(installerPath) || !File.Exists(installerPath))
        {
            return;
        }

        File.Delete(installerPath);
    }

    private static HttpRequestMessage CreateGitHubRequest(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("Fluxo-Updater");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        return request;
    }

    private static bool TrySelectInstallerAsset(
        JsonElement releaseRoot,
        out string assetName,
        out string downloadUrl)
    {
        assetName = string.Empty;
        downloadUrl = string.Empty;

        if (!releaseRoot.TryGetProperty("assets", out var assets)
            || assets.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var asset in assets.EnumerateArray())
        {
            var name = ReadString(asset, "name");
            var url = ReadString(asset, "browser_download_url");
            if (string.IsNullOrWhiteSpace(name)
                || string.IsNullOrWhiteSpace(url)
                || !InstallerAssetNameRegex().IsMatch(name))
            {
                continue;
            }

            assetName = name;
            downloadUrl = url;
            return true;
        }

        return false;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return property.GetString();
    }

    private static bool TryParseVersion(string? value, out Version version)
    {
        version = new Version();
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        var metadataIndex = normalized.IndexOf('+');
        if (metadataIndex >= 0)
        {
            normalized = normalized[..metadataIndex];
        }

        var prereleaseIndex = normalized.IndexOf('-');
        if (prereleaseIndex >= 0)
        {
            normalized = normalized[..prereleaseIndex];
        }

        return Version.TryParse(normalized, out version!);
    }

    private static async Task CopyToFileWithProgressAsync(
        Stream source,
        Stream destination,
        long? contentLength,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long totalBytesRead = 0;
        progress?.Report(0);

        while (true)
        {
            var bytesRead = await source.ReadAsync(buffer, cancellationToken);
            if (bytesRead == 0)
                break;

            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            totalBytesRead += bytesRead;

            var totalBytes = contentLength.GetValueOrDefault();
            if (totalBytes > 0)
            {
                var percentage = Math.Clamp(totalBytesRead * 100d / totalBytes, 0d, 100d);
                progress?.Report(percentage);
            }
        }

        progress?.Report(100);
    }

    [GeneratedRegex(@"^fluxo-.+-Installer\.exe$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InstallerAssetNameRegex();
}
