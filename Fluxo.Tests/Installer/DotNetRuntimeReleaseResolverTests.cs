using Fluxo.Installer.Services;
using Xunit;

namespace Fluxo.Tests.Installer;

public sealed class DotNetRuntimeReleaseResolverTests
{
    [Fact]
    public void ResolveLatestWindowsDesktopRuntimeInstaller_SelectsLatest10WinX64Exe()
    {
        const string indexJson = """
        {
          "releases-index": [
            {
              "channel-version": "10.0",
              "latest-release": "10.0.8",
              "releases.json": "https://example.test/10/releases.json"
            }
          ]
        }
        """;
        const string releasesJson = """
        {
          "releases": [
            {
              "release-version": "10.0.8",
              "windowsdesktop": {
                "files": [
                  {
                    "name": "windowsdesktop-runtime-win-x64.exe",
                    "rid": "win-x64",
                    "url": "https://example.test/windowsdesktop-runtime-10.0.8-win-x64.exe",
                    "hash": "abc123"
                  },
                  {
                    "name": "windowsdesktop-runtime-win-x86.exe",
                    "rid": "win-x86",
                    "url": "https://example.test/windowsdesktop-runtime-10.0.8-win-x86.exe",
                    "hash": "def456"
                  }
                ]
              }
            }
          ]
        }
        """;

        var result = DotNetRuntimeReleaseResolver.ResolveLatestWindowsDesktopRuntimeInstaller(
            indexJson,
            releasesJson,
            channelVersion: "10.0",
            rid: "win-x64");

        Assert.Equal("10.0.8", result.Version);
        Assert.Equal("win-x64", result.Rid);
        Assert.Equal("windowsdesktop-runtime-win-x64.exe", result.FileName);
        Assert.Equal("https://example.test/windowsdesktop-runtime-10.0.8-win-x64.exe", result.Url);
        Assert.Equal("abc123", result.Hash);
        Assert.Equal("https://example.test/10/releases.json", result.ReleasesJsonUrl);
    }

    [Fact]
    public void ResolveLatestWindowsDesktopRuntimeInstaller_Throws_WhenChannelMissing()
    {
        const string indexJson = """{ "releases-index": [] }""";
        const string releasesJson = """{ "releases": [] }""";

        var ex = Assert.Throws<InvalidOperationException>(() =>
            DotNetRuntimeReleaseResolver.ResolveLatestWindowsDesktopRuntimeInstaller(
                indexJson,
                releasesJson,
                channelVersion: "10.0",
                rid: "win-x64"));

        Assert.Contains("10.0", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveLatestWindowsDesktopRuntimeInstaller_Throws_WhenWinX64DesktopInstallerMissing()
    {
        const string indexJson = """
        {
          "releases-index": [
            {
              "channel-version": "10.0",
              "latest-release": "10.0.8",
              "releases.json": "https://example.test/10/releases.json"
            }
          ]
        }
        """;
        const string releasesJson = """
        {
          "releases": [
            {
              "release-version": "10.0.8",
              "windowsdesktop": { "files": [] }
            }
          ]
        }
        """;

        var ex = Assert.Throws<InvalidOperationException>(() =>
            DotNetRuntimeReleaseResolver.ResolveLatestWindowsDesktopRuntimeInstaller(
                indexJson,
                releasesJson,
                channelVersion: "10.0",
                rid: "win-x64"));

        Assert.Contains("Windows Desktop Runtime", ex.Message, StringComparison.Ordinal);
    }
}
