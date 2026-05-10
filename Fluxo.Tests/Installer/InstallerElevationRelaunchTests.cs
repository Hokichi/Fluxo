using Fluxo.Installer.Models;
using Xunit;

namespace Fluxo.Tests.Installer;

public sealed class InstallerElevationRelaunchTests : IDisposable
{
    private readonly string bundlePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.exe");

    public InstallerElevationRelaunchTests()
    {
        File.WriteAllText(bundlePath, string.Empty);
    }

    [Fact]
    public void ShouldRelaunch_InteractiveUnelevatedBundle()
    {
        var shouldRelaunch = InstallerElevationRelaunch.ShouldRelaunch(
            isInteractive: true,
            isElevated: false,
            originalBundlePath: bundlePath,
            currentProcessPath: @"C:\Users\Admins\AppData\Local\Temp\{bundle}\.ba\Fluxo.Installer.exe");

        Assert.True(shouldRelaunch);
    }

    [Fact]
    public void ShouldRelaunch_SkipsHeadlessOrAlreadyElevatedRuns()
    {
        Assert.False(InstallerElevationRelaunch.ShouldRelaunch(
            isInteractive: false,
            isElevated: false,
            originalBundlePath: bundlePath,
            currentProcessPath: @"C:\Temp\Fluxo.Installer.exe"));
        Assert.False(InstallerElevationRelaunch.ShouldRelaunch(
            isInteractive: true,
            isElevated: true,
            originalBundlePath: bundlePath,
            currentProcessPath: @"C:\Temp\Fluxo.Installer.exe"));
    }

    [Fact]
    public void ShouldRelaunch_SkipsWhenOriginalPathIsCurrentProcess()
    {
        Assert.False(InstallerElevationRelaunch.ShouldRelaunch(
            isInteractive: true,
            isElevated: false,
            originalBundlePath: bundlePath,
            currentProcessPath: bundlePath));
    }

    [Fact]
    public void CreateStartInfo_UsesRunAsShellVerb()
    {
        var startInfo = InstallerElevationRelaunch.CreateStartInfo(bundlePath);

        Assert.Equal(bundlePath, startInfo.FileName);
        Assert.True(startInfo.UseShellExecute);
        Assert.Equal("runas", startInfo.Verb);
    }

    public void Dispose()
    {
        File.Delete(bundlePath);
    }
}
