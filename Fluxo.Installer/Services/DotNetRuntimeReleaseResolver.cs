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
        var latestRelease = channel.GetProperty("latest-release").GetString()
            ?? throw new InvalidOperationException($"The .NET {channelVersion} release index did not include latest-release.");
        var releasesJsonUrl = channel.GetProperty("releases.json").GetString()
            ?? throw new InvalidOperationException($"The .NET {channelVersion} release index did not include releases.json.");

        using var releasesDocument = JsonDocument.Parse(releasesJson);
        foreach (var release in releasesDocument.RootElement.GetProperty("releases").EnumerateArray())
        {
            if (!string.Equals(release.GetProperty("release-version").GetString(), latestRelease, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var windowsDesktop = release.GetProperty("windowsdesktop");
            foreach (var file in windowsDesktop.GetProperty("files").EnumerateArray())
            {
                var name = file.GetProperty("name").GetString() ?? string.Empty;
                var fileRid = file.GetProperty("rid").GetString() ?? string.Empty;
                if (!string.Equals(fileRid, rid, StringComparison.OrdinalIgnoreCase)
                    || !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return new DotNetRuntimeInstallerInfo(
                    latestRelease,
                    fileRid,
                    name,
                    file.GetProperty("url").GetString() ?? throw new InvalidOperationException("Runtime installer URL was missing."),
                    file.TryGetProperty("hash", out var hash) ? hash.GetString() ?? string.Empty : string.Empty,
                    releasesJsonUrl);
            }
        }

        throw new InvalidOperationException($"Could not find a .NET {channelVersion} Windows Desktop Runtime installer for {rid}.");
    }

    public static string ResolveReleasesJsonUrl(string releasesIndexJson, string channelVersion = "10.0")
    {
        using var indexDocument = JsonDocument.Parse(releasesIndexJson);
        return FindChannel(indexDocument.RootElement, channelVersion).GetProperty("releases.json").GetString()
            ?? throw new InvalidOperationException($"The .NET {channelVersion} release index did not include releases.json.");
    }

    private static JsonElement FindChannel(JsonElement root, string channelVersion)
    {
        foreach (var channel in root.GetProperty("releases-index").EnumerateArray())
        {
            if (string.Equals(channel.GetProperty("channel-version").GetString(), channelVersion, StringComparison.OrdinalIgnoreCase))
            {
                return channel;
            }
        }

        throw new InvalidOperationException($"Could not find .NET release channel {channelVersion}.");
    }
}
