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
    public void AppFileHarvest_RemovesInstalledVersionRegistryKeyOnUninstall()
    {
        var wxs = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "Fluxo.Installer.Msi",
            "ExampleComponents.wxs"));

        Assert.Contains("<RemoveRegistryKey", wxs);
        Assert.Contains("Root=\"HKLM\"", wxs);
        Assert.Contains("Key=\"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\fluxo\"", wxs);
        Assert.Contains("Action=\"removeOnUninstall\"", wxs);
    }

    [Fact]
    public void Package_DeclaresPerMachineScope()
    {
        var wxs = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "Fluxo.Installer.Msi",
            "Package.wxs"));

        Assert.Contains("Scope=\"perMachine\"", wxs);
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

    [Fact]
    public void Bundle_ForcesMsiPackagePerMachine()
    {
        var wxs = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "Fluxo.Installer.Bundle",
            "Bundle.wxs"));

        Assert.Contains("ForcePerMachine=\"yes\"", wxs);
    }

    [Fact]
    public void BundleProject_DoesNotRewriteFinalBundleManifestAfterBuild()
    {
        var repositoryRoot = GetRepositoryRoot();
        var project = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Fluxo.Installer.Bundle",
            "Fluxo.Installer.Bundle.wixproj"));
        var scriptPath = Path.Combine(
            repositoryRoot,
            "Fluxo.Installer.Bundle",
            "RequireAdministratorBundleManifest.ps1");

        Assert.DoesNotContain("RequireAdministratorBundleManifest", project);
        Assert.DoesNotContain("outputresource:", project);
        Assert.False(File.Exists(scriptPath));
    }

    [Fact]
    public void InstallerExecutable_DoesNotRequestAdministratorManifest()
    {
        var repositoryRoot = GetRepositoryRoot();
        var project = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Fluxo.Installer",
            "Fluxo.Installer.csproj"));

        Assert.DoesNotContain("<ApplicationManifest>app.manifest</ApplicationManifest>", project);
        Assert.False(File.Exists(Path.Combine(repositoryRoot, "Fluxo.Installer", "app.manifest")));
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
