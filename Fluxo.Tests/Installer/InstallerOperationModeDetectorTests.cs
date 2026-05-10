using Fluxo.Installer.Models;
using Xunit;

namespace Fluxo.Tests.Installer;

public sealed class InstallerOperationModeDetectorTests
{
    [Fact]
    public void Detect_UsesRepairerOriginalBundleSourceBeforeExtractedProcessPath()
    {
        var mode = InstallerOperationModeDetector.Detect(
            originalSourcePath: @"C:\Program Files\fluxo\fluxo.Repairer.exe",
            sourceProcessPath: null,
            processPath: @"C:\Users\Admins\AppData\Local\Temp\{bundle}\Fluxo.Installer.exe");

        Assert.Equal(InstallerOperationMode.Maintenance, mode);
    }

    [Fact]
    public void Detect_UsesRepairerSourceProcessPath_WhenOriginalSourceIsRegisteredInstaller()
    {
        var mode = InstallerOperationModeDetector.Detect(
            originalSourcePath: @"C:\Users\Admins\source\repos\Fluxo\Fluxo.Installer.Bundle\bin\x64\Release\fluxo-1.0.0-Installer.exe",
            sourceProcessPath: @"F:\fluxo\fluxo.Repairer.exe",
            processPath: @"C:\Users\Admins\AppData\Local\Temp\{bundle}\Fluxo.Installer.exe");

        Assert.Equal(InstallerOperationMode.Maintenance, mode);
    }

    [Fact]
    public void Detect_UsesRepairExecutableName_WhenInstalledNameHasDifferentCasing()
    {
        var mode = InstallerOperationModeDetector.Detect(
            originalSourcePath: @"G:\fluxo\Fluxo.REPAIRER.exe",
            sourceProcessPath: null,
            processPath: @"C:\Users\Admins\AppData\Local\Temp\{bundle}\Fluxo.Installer.exe");

        Assert.Equal(InstallerOperationMode.Maintenance, mode);
    }

    [Fact]
    public void SelectBundleExecutablePathForViewModel_PrefersSourceProcess_WhenOriginalIsInstaller()
    {
        var path = InstallerOperationModeDetector.SelectBundleExecutablePathForViewModel(
            wixBundleSourceProcessPath: @"F:\fluxo\fluxo.Repairer.exe",
            wixBundleOriginalSource: @"C:\Downloads\fluxo-1.0.0-Installer.exe",
            fallbackBundlePath: @"C:\Downloads\fluxo-1.0.0-Installer.exe");

        Assert.Equal(@"F:\fluxo\fluxo.Repairer.exe", path);
    }

    [Fact]
    public void SelectBundleExecutablePathForViewModel_PrefersOriginal_WhenSourceProcessMissing()
    {
        var path = InstallerOperationModeDetector.SelectBundleExecutablePathForViewModel(
            wixBundleSourceProcessPath: null,
            wixBundleOriginalSource: @"C:\Program Files\fluxo\fluxo.Repairer.exe",
            fallbackBundlePath: @"C:\X\fluxo-1.0.0-Installer.exe");

        Assert.Equal(@"C:\Program Files\fluxo\fluxo.Repairer.exe", path);
    }

    [Fact]
    public void SelectBundleExecutablePathForViewModel_UsesFallback_WhenNeitherIsRepairer()
    {
        const string fallback = @"C:\Downloads\fluxo-1.0.0-Installer.exe";
        var path = InstallerOperationModeDetector.SelectBundleExecutablePathForViewModel(
            wixBundleSourceProcessPath: fallback,
            wixBundleOriginalSource: fallback,
            fallbackBundlePath: fallback);

        Assert.Equal(fallback, path);
    }
}
