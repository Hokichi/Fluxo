using System.IO;
using Xunit;

namespace Fluxo.Tests.Installer;

public sealed class InstallerMsiAuthoringTests
{
    [Fact]
    public void AppFileHarvest_ExcludesFluxoDatabase()
    {
        var wxs = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "Fluxo.Installer.Msi",
            "ExampleComponents.wxs"));

        Assert.Contains("<Exclude Files=\"$(var.FluxoAppOutputDir)\\**\\*.db\" />", wxs);
    }

    [Fact]
    public void Package_SequencesFilesBeforeRegistryActions()
    {
        var wxs = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "Fluxo.Installer.Msi",
            "Package.wxs"));

        Assert.Contains("<InstallFiles Sequence=\"4000\" />", wxs);
        Assert.Contains("<WriteRegistryValues Sequence=\"5000\" />", wxs);
        Assert.Contains("<RemoveFiles Sequence=\"3500\" />", wxs);
        Assert.Contains("<RemoveFolders Sequence=\"3600\" />", wxs);
        Assert.Contains("<RemoveRegistryValues Sequence=\"3650\" />", wxs);
    }

    private static string GetRepositoryRoot()
    {
        var directory = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(directory)
               && !File.Exists(Path.Combine(directory, "Fluxo.slnx")))
        {
            directory = Directory.GetParent(directory)?.FullName;
        }

        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new DirectoryNotFoundException("Could not find repository root.");
        }

        return directory;
    }
}
