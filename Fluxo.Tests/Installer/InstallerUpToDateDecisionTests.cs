using Fluxo.Installer.Models;
using Xunit;

namespace Fluxo.Tests.Installer;

public sealed class InstallerUpToDateDecisionTests
{
    [Fact]
    public void Install_SameVersion_ShouldSkip()
    {
        var skip = InstallerUpToDateDecision.ShouldSkipInstall(
            InstallerOperationMode.Install,
            detectStatus: 0,
            currentBundleVersion: "1.0.0.0",
            highestDetectedInstalledVersion: "1.0.0.0",
            compareVersions: static (_, _) => 0);

        Assert.True(skip);
    }

    [Fact]
    public void Install_HigherInstalledVersion_ShouldSkip()
    {
        var skip = InstallerUpToDateDecision.ShouldSkipInstall(
            InstallerOperationMode.Install,
            detectStatus: 0,
            currentBundleVersion: "1.0.0.0",
            highestDetectedInstalledVersion: "1.1.0.0",
            compareVersions: static (_, _) => 1);

        Assert.True(skip);
    }

    [Fact]
    public void Repair_SameVersion_ShouldNotSkip()
    {
        var skip = InstallerUpToDateDecision.ShouldSkipInstall(
            InstallerOperationMode.Maintenance,
            detectStatus: 0,
            currentBundleVersion: "1.0.0.0",
            highestDetectedInstalledVersion: "1.0.0.0",
            compareVersions: static (_, _) => 0);

        Assert.False(skip);
    }

    [Fact]
    public void Uninstall_HigherInstalledVersion_ShouldNotSkip()
    {
        var skip = InstallerUpToDateDecision.ShouldSkipInstall(
            InstallerOperationMode.Uninstall,
            detectStatus: 0,
            currentBundleVersion: "1.0.0.0",
            highestDetectedInstalledVersion: "2.0.0.0",
            compareVersions: static (_, _) => 1);

        Assert.False(skip);
    }
}
