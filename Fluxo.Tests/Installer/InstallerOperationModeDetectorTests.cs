using Fluxo.Installer.Models;
using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Installer;

public sealed class InstallerOperationModeDetectorTests
{
    [Fact]
    public void Detect_UsesRepairerOriginalBundleSourceBeforeExtractedProcessPath()
    {
        var mode = InstallerOperationModeDetector.Detect(
            originalSourcePath: WindowsPathFixtures.RepairerExecutable,
            sourceProcessPath: null,
            processPath: WindowsPathFixtures.ExtractedBundleProcess);

        Assert.Equal(InstallerOperationMode.Maintenance, mode);
    }

    [Fact]
    public void Detect_UsesRepairerSourceProcessPath_WhenOriginalSourceIsRegisteredInstaller()
    {
        var mode = InstallerOperationModeDetector.Detect(
            originalSourcePath: WindowsPathFixtures.BuildOutputInstaller,
            sourceProcessPath: WindowsPathFixtures.AlternateRepairerExecutable,
            processPath: WindowsPathFixtures.ExtractedBundleProcess);

        Assert.Equal(InstallerOperationMode.Maintenance, mode);
    }

    [Fact]
    public void Detect_UsesRepairExecutableName_WhenInstalledNameHasDifferentCasing()
    {
        var mode = InstallerOperationModeDetector.Detect(
            originalSourcePath: WindowsPathFixtures.UppercaseRepairerExecutable,
            sourceProcessPath: null,
            processPath: WindowsPathFixtures.ExtractedBundleProcess);

        Assert.Equal(InstallerOperationMode.Maintenance, mode);
    }

    [Fact]
    public void SelectBundleExecutablePathForViewModel_PrefersSourceProcess_WhenOriginalIsInstaller()
    {
        var path = InstallerOperationModeDetector.SelectBundleExecutablePathForViewModel(
            wixBundleSourceProcessPath: WindowsPathFixtures.AlternateRepairerExecutable,
            wixBundleOriginalSource: WindowsPathFixtures.DownloadsInstaller,
            fallbackBundlePath: WindowsPathFixtures.DownloadsInstaller);

        Assert.Equal(WindowsPathFixtures.AlternateRepairerExecutable, path);
    }

    [Fact]
    public void SelectBundleExecutablePathForViewModel_PrefersOriginal_WhenSourceProcessMissing()
    {
        var path = InstallerOperationModeDetector.SelectBundleExecutablePathForViewModel(
            wixBundleSourceProcessPath: null,
            wixBundleOriginalSource: WindowsPathFixtures.RepairerExecutable,
            fallbackBundlePath: WindowsPathFixtures.TempFile("fluxo-1.0.0-Installer.exe"));

        Assert.Equal(WindowsPathFixtures.RepairerExecutable, path);
    }

    [Fact]
    public void SelectBundleExecutablePathForViewModel_UsesFallback_WhenNeitherIsRepairer()
    {
        var fallback = WindowsPathFixtures.DownloadsInstaller;
        var path = InstallerOperationModeDetector.SelectBundleExecutablePathForViewModel(
            wixBundleSourceProcessPath: fallback,
            wixBundleOriginalSource: fallback,
            fallbackBundlePath: fallback);

        Assert.Equal(fallback, path);
    }
}
