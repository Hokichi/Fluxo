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
    public void AppFileHarvest_WritesInstallLocationRegistryValue()
    {
        var wxs = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "Fluxo.Installer.Msi",
            "ExampleComponents.wxs"));

        Assert.Contains("Name=\"InstallLocation\"", wxs);
        Assert.Contains("Value=\"[INSTALLFOLDER]\"", wxs);
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
    public void Package_EmbedsCabinetPayloadsInsideMsi()
    {
        var wxs = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "Fluxo.Installer.Msi",
            "Package.wxs"));

        Assert.Contains("<MediaTemplate EmbedCab=\"yes\" />", wxs);
    }

    [Fact]
    public void Package_EnablesMsiMajorUpgradeRemoval()
    {
        var wxs = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "Fluxo.Installer.Msi",
            "Package.wxs"));

        Assert.Contains("UpgradeStrategy=\"majorUpgrade\"", wxs);
        Assert.Contains("UpgradeCode=\"{66DCBC7E-8077-4078-81C9-5618245C0435}\"", wxs);
        Assert.DoesNotContain("UpgradeStrategy=\"none\"", wxs);
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
        Assert.Contains("Condition=\"REMOVE~=&quot;ALL&quot; AND NOT UPGRADINGPRODUCTCODE\"", wxs);
        Assert.Contains(@"reg.exe delete HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\fluxo /f", wxs);
    }

    [Fact]
    public void Folders_DoesNotDeclareMachineWideProgramDataFolder()
    {
        var wxs = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "Fluxo.Installer.Msi",
            "Folders.wxs"));

        Assert.DoesNotContain("CommonAppDataFolder", wxs);
        Assert.DoesNotContain("FLUXOPROGRAMDATAFOLDER", wxs);
        Assert.DoesNotContain("FluxoProgramDataFolderComponent", wxs);
    }

    [Fact]
    public void Package_DoesNotRepairProgramDataAclDuringInstallOrRepair()
    {
        var wxs = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "Fluxo.Installer.Msi",
            "Package.wxs"));

        Assert.DoesNotContain("RepairFluxoProgramDataAcl", wxs);
        Assert.DoesNotContain("icacls.exe", wxs);
        Assert.DoesNotContain("%ProgramData%\\fluxo", wxs);
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
    public void Bundle_KeepsMsiPackageCachedForMaintenance()
    {
        var wxs = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "Fluxo.Installer.Bundle",
            "Bundle.wxs"));

        Assert.Contains("Cache=\"keep\"", wxs);
    }

    [Fact]
    public void MsiProject_BuildsFrameworkDependentAppOutput()
    {
        var project = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "Fluxo.Installer.Msi",
            "Fluxo.Installer.Msi.wixproj"));

        Assert.Contains(@"<FluxoAppOutputDir>$(MSBuildThisFileDirectory)..\Fluxo\bin\$(Configuration)\net10.0-windows\win-x64\</FluxoAppOutputDir>", project);
        Assert.Contains("Name=\"BuildFluxoApplicationForInstaller\"", project);
        Assert.Contains("Targets=\"Restore;Build\"", project);
        Assert.Contains("RuntimeIdentifier=win-x64", project);
        Assert.Contains("SelfContained=false", project);
        Assert.DoesNotContain("Name=\"PublishFluxoApplicationForInstaller\"", project);
        Assert.DoesNotContain("Targets=\"Restore;Publish\"", project);
        Assert.DoesNotContain("SelfContained=true", project);
        Assert.DoesNotContain("PublishSelfContained=true", project);
        Assert.DoesNotContain("PublishSingleFile=false", project);
    }

    [Fact]
    public void BundleProject_BuildsSelfContainedManagedBootstrapperOutput()
    {
        var project = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "Fluxo.Installer.Bundle",
            "Fluxo.Installer.Bundle.wixproj"));

        Assert.Contains(@"<ManagedBootstrapperExe>$(MSBuildThisFileDirectory)..\Fluxo.Installer\bin\$(Configuration)\net10.0-windows\win-x64\Fluxo.Installer.exe</ManagedBootstrapperExe>", project);
        Assert.Contains(@"<ManagedBootstrapperOutputDir>$(MSBuildThisFileDirectory)..\Fluxo.Installer\bin\$(Configuration)\net10.0-windows\win-x64\</ManagedBootstrapperOutputDir>", project);
        Assert.Contains("Name=\"BuildManagedBootstrapperForBundle\"", project);
        Assert.Contains("Targets=\"Restore;Build\"", project);
        Assert.Contains("RuntimeIdentifier=win-x64", project);
        Assert.Contains("SelfContained=true", project);
        Assert.DoesNotContain("Name=\"PublishManagedBootstrapperForBundle\"", project);
        Assert.DoesNotContain("Targets=\"Restore;Publish\"", project);
        Assert.DoesNotContain("PublishSelfContained=true", project);
        Assert.DoesNotContain("PublishSingleFile=false", project);
        Assert.DoesNotContain(@"\publish\", project);
        Assert.DoesNotContain(
            @"<ProjectReference Include=""..\Fluxo.Installer\Fluxo.Installer.csproj"" />",
            project);
    }

    [Fact]
    public void BundleProject_GeneratesManagedBootstrapperPayloadGroup()
    {
        var project = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "Fluxo.Installer.Bundle",
            "Fluxo.Installer.Bundle.wixproj"));

        Assert.Contains("<GeneratedBootstrapperPayloadsWxs>$(MSBuildProjectDirectory)\\obj\\$(Configuration)\\GeneratedBootstrapperPayloads.wxs</GeneratedBootstrapperPayloadsWxs>", project);
        Assert.Contains("<Compile Include=\"$(GeneratedBootstrapperPayloadsWxs)\" />", project);
        Assert.Contains("Name=\"GenerateManagedBootstrapperPayloads\"", project);
        Assert.Contains("DependsOnTargets=\"BuildManagedBootstrapperForBundle\"", project);
        Assert.Contains("ManagedBootstrapperPayloadFiles", project);
        Assert.Contains("Include=\"$(ManagedBootstrapperOutputDir)**\\*\"", project);
        Assert.Contains("Exclude=\"$(ManagedBootstrapperExe);$(ManagedBootstrapperOutputDir)publish\\**\"", project);
        Assert.Contains("PayloadGroup Id=\"ManagedBootstrapperPayloads\"", project);
    }

    [Fact]
    public void Bundle_ReferencesGeneratedManagedBootstrapperPayloadGroup()
    {
        var wxs = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "Fluxo.Installer.Bundle",
            "Bundle.wxs"));

        Assert.Contains("<PayloadGroupRef Id=\"ManagedBootstrapperPayloads\" />", wxs);
        Assert.DoesNotContain("<Payload SourceFile=\"$(var.ManagedBootstrapperOutputDir)", wxs);
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

    [Fact]
    public void Bootstrapper_DoesNotSuppressRelatedBundleUpgradeCleanup()
    {
        var source = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "Fluxo.Installer",
            "BootstrapperEntry.cs"));

        Assert.DoesNotContain("RelatedBundlePlanType.None", source);
        Assert.DoesNotContain("RequestState.None", source);
        Assert.DoesNotContain("PlanRelatedBundleType +=", source);
        Assert.DoesNotContain("PlanRelatedBundle +=", source);
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
