using Fluxo.Installer.Models;
using Fluxo.Installer.Services;
using Fluxo.Installer.ViewModels;
using Xunit;

namespace Fluxo.Tests.Installer;

public sealed class InstallerLaunchCommandTests
{
    [Fact]
    public void LaunchApp_UsesInstalledExePath()
    {
        string? launchedPath = null;
        var vm = new InstallerViewModel(
            dotNetRuntimeDetector: new FixedRuntimeDetector(true),
            fileExists: static _ => true,
            bundleExecutablePath: @"C:\Temp\fluxo-installer.exe",
            copyFile: static (_, _, _) => { },
            launchInstalledApp: path => launchedPath = path);

        vm.OnApplyComplete(0);

        Assert.Equal(InstallerState.FinishedSuccess, vm.State);
        Assert.True(vm.LaunchAppCommand.CanExecute(null));

        vm.LaunchAppCommand.Execute(null);

        Assert.Equal("C:\\Program Files\\fluxo\\Fluxo.exe", launchedPath);
    }

    [Fact]
    public void LaunchApp_Disabled_WhenInstallFailed()
    {
        var launchCalls = 0;
        var vm = new InstallerViewModel(
            dotNetRuntimeDetector: new FixedRuntimeDetector(true),
            fileExists: static _ => false,
            bundleExecutablePath: @"C:\Temp\fluxo-installer.exe",
            copyFile: static (_, _, _) => { },
            launchInstalledApp: _ => launchCalls++);

        vm.OnApplyComplete(0);

        Assert.Equal(InstallerState.FinishedFailed, vm.State);
        Assert.False(vm.LaunchAppCommand.CanExecute(null));

        vm.LaunchAppCommand.Execute(null);

        Assert.Equal(0, launchCalls);
    }

    [Fact]
    public void LaunchApp_Enabled_WhenVersionIsUpToDate()
    {
        string? launchedPath = null;
        var vm = new InstallerViewModel(
            dotNetRuntimeDetector: new FixedRuntimeDetector(true),
            fileExists: static _ => true,
            bundleExecutablePath: @"C:\Temp\fluxo-installer.exe",
            copyFile: static (_, _, _) => { },
            launchInstalledApp: path => launchedPath = path);

        vm.OnDetectedUpToDateVersion();

        Assert.Equal(InstallerState.FinishedUpToDate, vm.State);
        Assert.True(vm.LaunchAppCommand.CanExecute(null));

        vm.LaunchAppCommand.Execute(null);

        Assert.Equal("C:\\Program Files\\fluxo\\Fluxo.exe", launchedPath);
    }

    [Fact]
    public void LaunchApp_Disabled_WhenUninstallCompleted()
    {
        var launchCalls = 0;
        var vm = new InstallerViewModel(
            dotNetRuntimeDetector: new FixedRuntimeDetector(true),
            operationMode: InstallerOperationMode.Uninstall,
            fileExists: static _ => true,
            bundleExecutablePath: @"C:\Temp\fluxo-installer.exe",
            copyFile: static (_, _, _) => { },
            launchInstalledApp: _ => launchCalls++);

        vm.Begin();
        vm.OnApplyComplete(0);

        Assert.Equal(InstallerState.FinishedUninstalled, vm.State);
        Assert.False(vm.LaunchAppCommand.CanExecute(null));

        vm.LaunchAppCommand.Execute(null);
        Assert.Equal(0, launchCalls);
    }

    private sealed class FixedRuntimeDetector(bool isInstalled) : IDotNetRuntimeDetector
    {
        public bool IsRequiredRuntimeInstalled() => isInstalled;
    }
}
