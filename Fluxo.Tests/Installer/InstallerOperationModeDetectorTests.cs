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
}
