using Fluxo.Services.Updates;
using Xunit;

namespace Fluxo.Tests.Services.Updates;

public sealed class AppUpdateInteractionServiceTests
{
    [Fact]
    public void BuildAvailableUpdatePrompt_UsesLatestVersion()
    {
        var update = AppUpdateCheckResult.UpdateAvailable(
            "2.5.0",
            "fluxo-2.5.0-Installer.exe",
            "https://example.test/fluxo-2.5.0-Installer.exe");

        var prompt = AppUpdateInteractionService.BuildAvailableUpdatePrompt(update);

        Assert.Equal("Fluxo 2.5.0 is available. Download and install it?", prompt);
    }

    [Fact]
    public void BuildAvailableUpdatePrompt_FallsBackToUnknown_WhenLatestVersionMissing()
    {
        var update = AppUpdateCheckResult.UpdateAvailable(
            " ",
            "fluxo-2.5.0-Installer.exe",
            "https://example.test/fluxo-2.5.0-Installer.exe");

        var prompt = AppUpdateInteractionService.BuildAvailableUpdatePrompt(update);

        Assert.Equal("Fluxo Unknown is available. Download and install it?", prompt);
    }
}
