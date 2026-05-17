using Fluxo.Installer.Models;
using Fluxo.Installer.Services;
using Fluxo.Installer.ViewModels;
using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Installer;

public sealed class DotNetRuntimeDetectorTests
{
    [Fact]
    public void ReturnsFalse_When_RuntimeMissing()
    {
        var detector = new DotNetRuntimeDetector(
            requiredMajorVersion: 10,
            runtimeListProvider: static () => WindowsPathFixtures.RuntimeListEntry(9));

        var result = detector.IsRequiredRuntimeInstalled();

        Assert.False(result);
    }

    [Fact]
    public void ReturnsTrue_When_RuntimePresent()
    {
        var detector = new DotNetRuntimeDetector(
            requiredMajorVersion: 10,
            runtimeListProvider: static () => WindowsPathFixtures.RuntimeListEntry(10));

        var result = detector.IsRequiredRuntimeInstalled();

        Assert.True(result);
    }

    [Fact]
    public void InstallCommand_FailFast_When_RuntimeMissing()
    {
        var vm = new InstallerViewModel(new FixedRuntimeDetector(false));

        vm.InstallCommand.Execute(null);

        Assert.Equal(InstallerState.FinishedFailed, vm.State);
        Assert.Equal(".NET Runtime is required. Install it, then run setup again.", vm.StatusMessage);
        Assert.Equal("Installation failed", vm.FinishedTitle);
    }

    [Fact]
    public void ReturnsFalse_When_RuntimeListProviderReturnsNull()
    {
        var detector = new DotNetRuntimeDetector(
            requiredMajorVersion: 10,
            runtimeListProvider: static () => null);

        var result = detector.IsRequiredRuntimeInstalled();

        Assert.False(result);
    }

    [Fact]
    public void ReturnsFalse_When_RuntimeListProviderThrows()
    {
        var detector = new DotNetRuntimeDetector(
            requiredMajorVersion: 10,
            runtimeListProvider: static () => throw new InvalidOperationException("provider failure"));

        var result = detector.IsRequiredRuntimeInstalled();

        Assert.False(result);
    }

    private sealed class FixedRuntimeDetector(bool isInstalled) : IDotNetRuntimeDetector
    {
        public bool IsRequiredRuntimeInstalled() => isInstalled;
    }
}
