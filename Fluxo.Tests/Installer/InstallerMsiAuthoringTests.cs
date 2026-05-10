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

        Assert.Contains("Name=\"InstalledVersion\"", wxs);
        Assert.Contains("Root=\"HKLM\"", wxs);
        Assert.Contains("Key=\"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\fluxo\"", wxs);
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
    public void Package_SchedulesDeferredRegistryCleanupOnUninstall()
    {
        var wxs = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "Fluxo.Installer.Msi",
            "Package.wxs"));

        Assert.Contains("RemoveFluxoInstalledVersionRegistryKey", wxs);
        Assert.Contains("Execute=\"deferred\"", wxs);
        Assert.Contains("Impersonate=\"no\"", wxs);
        Assert.Contains(@"reg.exe delete HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\fluxo /f", wxs);
    }

    [Fact]
    public void Folders_DeclaresMachineWideProgramDataFolder()
    {
        var wxs = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "Fluxo.Installer.Msi",
            "Folders.wxs"));

        Assert.Contains("CommonAppDataFolder", wxs);
        Assert.Contains("FLUXOPROGRAMDATAFOLDER", wxs);
        Assert.Contains("Name=\"fluxo\"", wxs);
        Assert.Contains("CreateFolder", wxs);
    }

    [Fact]
    public void Package_RepairsProgramDataAclDuringInstallAndRepair()
    {
        var wxs = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "Fluxo.Installer.Msi",
            "Package.wxs"));

        Assert.Contains("RepairFluxoProgramDataAcl", wxs);
        Assert.Contains("icacls.exe", wxs);
        Assert.Contains("*S-1-5-32-545:(OI)(CI)M", wxs);
        Assert.Contains("Execute=\"deferred\"", wxs);
        Assert.Contains("Impersonate=\"no\"", wxs);
        Assert.Contains("NOT REMOVE~=&quot;ALL&quot;", wxs);
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
    public void InstallerExecutable_DoesNotEmbedAdministratorManifest()
    {
        var repositoryRoot = GetRepositoryRoot();
        var installerDir = Path.Combine(repositoryRoot, "Fluxo.Installer");
        var project = File.ReadAllText(Path.Combine(installerDir, "Fluxo.Installer.csproj"));

        Assert.DoesNotContain("<ApplicationManifest>", project);
        Assert.False(File.Exists(Path.Combine(installerDir, "app.manifest")));
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
