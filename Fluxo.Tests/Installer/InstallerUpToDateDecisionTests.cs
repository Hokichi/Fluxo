using Fluxo.Installer.Models;
using Xunit;

namespace Fluxo.Tests.Installer;

public sealed class InstallerUpToDateDecisionTests
{
    [Theory]
    [InlineData("1.0.0.0", "1.0.0.0", 0)]
    [InlineData("1.0.0.0", "1.1.0.0", 1)]
    public void Install_WhenDetectedVersionIsSameOrHigher_ShouldSkip_AndCompareUsesInstalledThenCurrent(
        string currentBundleVersion,
        string highestDetectedInstalledVersion,
        int compareResult)
    {
        string? actualLeft = null;
        string? actualRight = null;

        var skip = InstallerUpToDateDecision.ShouldSkipInstall(
            InstallerOperationMode.Install,
            detectStatus: 0,
            currentBundleVersion: currentBundleVersion,
            highestDetectedInstalledVersion: highestDetectedInstalledVersion,
            compareVersions: (left, right) =>
            {
                actualLeft = left;
                actualRight = right;
                return compareResult;
            });

        Assert.True(skip);
        Assert.Equal(highestDetectedInstalledVersion, actualLeft);
        Assert.Equal(currentBundleVersion, actualRight);
    }

    [Theory]
    [InlineData(InstallerOperationMode.Maintenance, "1.0.0.0")]
    [InlineData(InstallerOperationMode.Uninstall, "2.0.0.0")]
    public void MaintenanceOrUninstall_ShouldNotSkip(
        InstallerOperationMode operationMode,
        string highestDetectedInstalledVersion)
    {
        var compareCalled = false;
        var skip = InstallerUpToDateDecision.ShouldSkipInstall(
            operationMode,
            detectStatus: 0,
            currentBundleVersion: "1.0.0.0",
            highestDetectedInstalledVersion: highestDetectedInstalledVersion,
            compareVersions: (_, _) =>
            {
                compareCalled = true;
                return 1;
            });

        Assert.False(skip);
        Assert.False(compareCalled);
    }
}
