using System.Text.Json;

namespace Fluxo.Installer.Services;

public sealed record DotNetRuntimeInstallerInfo(
    string Version,
    string Rid,
    string FileName,
    string Url,
    string Hash,
    string ReleasesJsonUrl);

public static class DotNetRuntimeReleaseResolver
{
    public static DotNetRuntimeInstallerInfo ResolveLatestWindowsDesktopRuntimeInstaller(
        string releasesIndexJson,
        string releasesJson,
        string channelVersion = "10.0",
        string rid = "win-x64")
    {
        using var indexDocument = JsonDocument.Parse(releasesIndexJson);
        var channel = FindChannel(indexDocument.RootElement, channelVersion);
        var latestRelease = GetRequiredString(channel, "latest-release", $"The .NET {channelVersion} release index did not include latest-release.");
        var releasesJsonUrl = GetRequiredString(channel, "releases.json", $"The .NET {channelVersion} release index did not include releases.json.");

        using var releasesDocument = JsonDocument.Parse(releasesJson);
        var releases = GetRequiredArray(releasesDocument.RootElement, "releases", $"The .NET {channelVersion} release metadata did not include releases.");
        foreach (var release in releases.EnumerateArray())
        {
            if (!release.TryGetProperty("release-version", out var releaseVersionElement)
                || !string.Equals(releaseVersionElement.GetString(), latestRelease, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var windowsDesktop = GetRequiredObject(
                release,
                "windowsdesktop",
                $"The .NET {channelVersion} release {latestRelease} did not include windowsdesktop metadata.");
            var files = GetRequiredArray(
                windowsDesktop,
                "files",
                $"The .NET {channelVersion} release {latestRelease} did not include windowsdesktop files metadata.");
            foreach (var file in files.EnumerateArray())
            {
                var name = file.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? string.Empty : string.Empty;
                var fileRid = file.TryGetProperty("rid", out var ridElement) ? ridElement.GetString() ?? string.Empty : string.Empty;
                if (!string.Equals(fileRid, rid, StringComparison.OrdinalIgnoreCase)
                    || !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return new DotNetRuntimeInstallerInfo(
                    latestRelease,
                    fileRid,
                    name,
                    GetRequiredString(file, "url", $"Runtime installer URL was missing for {name}."),
                    GetRequiredString(file, "hash", $"Runtime installer hash was missing for {name}."),
                    releasesJsonUrl);
            }

            throw new InvalidOperationException($"Could not find a .NET {channelVersion} Windows Desktop Runtime installer for {rid}.");
        }

        throw new InvalidOperationException($"Could not find .NET {channelVersion} release {latestRelease} in releases metadata.");
    }

    public static string ResolveReleasesJsonUrl(string releasesIndexJson, string channelVersion = "10.0")
    {
        using var indexDocument = JsonDocument.Parse(releasesIndexJson);
        return GetRequiredString(
            FindChannel(indexDocument.RootElement, channelVersion),
            "releases.json",
            $"The .NET {channelVersion} release index did not include releases.json.");
    }

    private static JsonElement FindChannel(JsonElement root, string channelVersion)
    {
        var releasesIndex = GetRequiredArray(root, "releases-index", "The .NET release index did not include releases-index.");
        foreach (var channel in releasesIndex.EnumerateArray())
        {
            if (channel.TryGetProperty("channel-version", out var channelVersionElement)
                && string.Equals(channelVersionElement.GetString(), channelVersion, StringComparison.OrdinalIgnoreCase))
            {
                return channel;
            }
        }

        throw new InvalidOperationException($"Could not find .NET release channel {channelVersion}.");
    }

    private static JsonElement GetRequiredObject(JsonElement element, string propertyName, string errorMessage)
    {
        if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Object)
        {
            return property;
        }

        throw new InvalidOperationException(errorMessage);
    }

    private static JsonElement GetRequiredArray(JsonElement element, string propertyName, string errorMessage)
    {
        if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Array)
        {
            return property;
        }

        throw new InvalidOperationException(errorMessage);
    }

    private static string GetRequiredString(JsonElement element, string propertyName, string errorMessage)
    {
        if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
        {
            var value = property.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        throw new InvalidOperationException(errorMessage);
    }
}
