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
        using var indexDocument = ParseJsonDocument(
            releasesIndexJson,
            "The .NET release index JSON is invalid.");
        var channel = FindChannel(indexDocument.RootElement, channelVersion);
        var latestRelease = GetRequiredString(
            channel,
            "latest-release",
            $"The .NET {channelVersion} release index latest-release must be a string.");
        var releasesJsonUrl = GetRequiredString(
            channel,
            "releases.json",
            $"The .NET {channelVersion} release index releases.json must be a string.");

        using var releasesDocument = ParseJsonDocument(
            releasesJson,
            $"The .NET {channelVersion} releases JSON is invalid.");
        EnsureValueKind(
            releasesDocument.RootElement,
            JsonValueKind.Object,
            $"The .NET {channelVersion} releases JSON root must be an object.");
        var releases = GetRequiredArray(
            releasesDocument.RootElement,
            "releases",
            $"The .NET {channelVersion} release metadata releases must be an array.");
        foreach (var release in releases.EnumerateArray())
        {
            EnsureValueKind(
                release,
                JsonValueKind.Object,
                $"The .NET {channelVersion} release metadata entry must be an object.");
            var releaseVersion = GetRequiredString(
                release,
                "release-version",
                $"The .NET {channelVersion} release metadata release-version must be a string.");
            if (!string.Equals(releaseVersion, latestRelease, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var windowsDesktop = GetRequiredObject(
                release,
                "windowsdesktop",
                $"The .NET {channelVersion} release {latestRelease} windowsdesktop metadata must be an object.");
            var files = GetRequiredArray(
                windowsDesktop,
                "files",
                $"The .NET {channelVersion} release {latestRelease} windowsdesktop files metadata must be an array.");
            foreach (var file in files.EnumerateArray())
            {
                EnsureValueKind(
                    file,
                    JsonValueKind.Object,
                    $"The .NET {channelVersion} release {latestRelease} windowsdesktop file metadata entry must be an object.");
                var name = GetRequiredString(
                    file,
                    "name",
                    $"The .NET {channelVersion} release {latestRelease} windowsdesktop file name must be a string.");
                var fileRid = GetRequiredString(
                    file,
                    "rid",
                    $"The .NET {channelVersion} release {latestRelease} windowsdesktop file rid must be a string.");
                if (!string.Equals(fileRid, rid, StringComparison.OrdinalIgnoreCase)
                    || !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var url = GetRequiredString(
                    file,
                    "url",
                    $"Runtime installer URL must be a string for {name}.");
                var hash = GetRequiredString(
                    file,
                    "hash",
                    $"Runtime installer hash must be a string for {name}.");

                return new DotNetRuntimeInstallerInfo(
                    latestRelease,
                    fileRid,
                    name,
                    url,
                    hash,
                    releasesJsonUrl);
            }

            throw new InvalidOperationException($"Could not find a .NET {channelVersion} Windows Desktop Runtime installer for {rid}.");
        }

        throw new InvalidOperationException($"Could not find .NET {channelVersion} release {latestRelease} in releases metadata.");
    }

    public static string ResolveReleasesJsonUrl(string releasesIndexJson, string channelVersion = "10.0")
    {
        using var indexDocument = ParseJsonDocument(
            releasesIndexJson,
            "The .NET release index JSON is invalid.");
        return GetRequiredString(
            FindChannel(indexDocument.RootElement, channelVersion),
            "releases.json",
            $"The .NET {channelVersion} release index releases.json must be a string.");
    }

    private static JsonElement FindChannel(JsonElement root, string channelVersion)
    {
        EnsureValueKind(
            root,
            JsonValueKind.Object,
            "The .NET release index JSON root must be an object.");
        var releasesIndex = GetRequiredArray(
            root,
            "releases-index",
            "The .NET release index releases-index must be an array.");
        foreach (var channel in releasesIndex.EnumerateArray())
        {
            EnsureValueKind(
                channel,
                JsonValueKind.Object,
                "The .NET release index channel entry must be an object.");
            var currentChannelVersion = GetRequiredString(
                channel,
                "channel-version",
                "The .NET release index channel-version must be a string.");
            if (string.Equals(currentChannelVersion, channelVersion, StringComparison.OrdinalIgnoreCase))
            {
                return channel;
            }
        }

        throw new InvalidOperationException($"Could not find .NET release channel {channelVersion}.");
    }

    private static JsonDocument ParseJsonDocument(string json, string errorMessage)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(errorMessage, ex);
        }
    }

    private static JsonElement GetRequiredObject(JsonElement element, string propertyName, string errorMessage)
    {
        EnsureValueKind(element, JsonValueKind.Object, errorMessage);

        if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Object)
        {
            return property;
        }

        throw new InvalidOperationException(errorMessage);
    }

    private static JsonElement GetRequiredArray(JsonElement element, string propertyName, string errorMessage)
    {
        EnsureValueKind(element, JsonValueKind.Object, errorMessage);

        if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Array)
        {
            return property;
        }

        throw new InvalidOperationException(errorMessage);
    }

    private static string GetRequiredString(JsonElement element, string propertyName, string errorMessage)
    {
        EnsureValueKind(element, JsonValueKind.Object, errorMessage);

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

    private static void EnsureValueKind(JsonElement element, JsonValueKind expectedKind, string errorMessage)
    {
        if (element.ValueKind != expectedKind)
        {
            throw new InvalidOperationException(errorMessage);
        }
    }
}
