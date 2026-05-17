using System.Net;
using System.Text;
using Fluxo.Services.Updates;
using Xunit;

namespace Fluxo.Tests.Services.Updates;

public sealed class AppUpdateServiceTests
{
    [Fact]
    public async Task CheckForUpdatesAsync_WhenLatestReleaseIsNewer_ReturnsInstallerAsset()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "tag_name": "v1.2.0",
                  "assets": [
                    {
                      "name": "fluxo-1.2.0-Installer.exe",
                      "browser_download_url": "https://example.test/fluxo-1.2.0-Installer.exe"
                    }
                  ]
                }
                """,
                Encoding.UTF8,
                "application/json")
        });

        var sut = new AppUpdateService(new HttpClient(handler));

        var result = await sut.CheckForUpdatesAsync("1.0.0");

        Assert.Equal(AppUpdateCheckStatus.UpdateAvailable, result.Status);
        Assert.Equal("1.2.0", result.LatestVersion);
        Assert.Equal("fluxo-1.2.0-Installer.exe", result.InstallerAssetName);
        Assert.Equal("https://example.test/fluxo-1.2.0-Installer.exe", result.InstallerDownloadUrl);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenLatestReleaseIsSameVersion_ReturnsUpToDate()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "tag_name": "v1.0.0",
                  "assets": [
                    {
                      "name": "fluxo-1.0.0-Installer.exe",
                      "browser_download_url": "https://example.test/fluxo-1.0.0-Installer.exe"
                    }
                  ]
                }
                """,
                Encoding.UTF8,
                "application/json")
        });

        var sut = new AppUpdateService(new HttpClient(handler));

        var result = await sut.CheckForUpdatesAsync("1.0.0");

        Assert.Equal(AppUpdateCheckStatus.UpToDate, result.Status);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenNetworkFails_ReturnsConnectionError()
    {
        var sut = new AppUpdateService(new HttpClient(
            new StubHttpMessageHandler(_ => throw new HttpRequestException("network unavailable"))));

        var result = await sut.CheckForUpdatesAsync("1.0.0");

        Assert.Equal(AppUpdateCheckStatus.Error, result.Status);
        Assert.Contains("internet", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DownloadInstallerAsync_WritesInstallerToTempFile()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"fluxo-update-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            var handler = new StubHttpMessageHandler(request =>
            {
                Assert.Equal("https://example.test/fluxo-1.2.0-Installer.exe", request.RequestUri?.ToString());
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent([1, 2, 3, 4])
                };
            });

            var sut = new AppUpdateService(new HttpClient(handler), () => tempDirectory);

            var path = await sut.DownloadInstallerAsync(
                "https://example.test/fluxo-1.2.0-Installer.exe",
                "fluxo-1.2.0-Installer.exe");

            Assert.Equal(Path.Combine(tempDirectory, "fluxo-1.2.0-Installer.exe"), path);
            Assert.Equal([1, 2, 3, 4], await File.ReadAllBytesAsync(path));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(responseFactory(request));
        }
    }
}
