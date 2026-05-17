using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Fluxo.Tests.Packaging;

public sealed class ExecutableNamingTests
{
    [Fact]
    public void AppProject_DeclaresLowercaseExecutableName_AndPreservesRootNamespace()
    {
        var project = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "Fluxo",
            "Fluxo.csproj"));

        var normalizedProject = string.Join("\n", project
            .Replace("\r\n", "\n")
            .Split('\n')
            .Select(line => line.Trim()));
        var expectedSequence = string.Join("\n",
            "<AssemblyTitle>fluxo</AssemblyTitle>",
            "<AssemblyName>fluxo</AssemblyName>",
            "<RootNamespace>Fluxo</RootNamespace>");

        Assert.Contains(expectedSequence, normalizedProject);
    }

    [Fact]
    public void AppProject_CleansExactLegacyPrimaryArtifactsWithoutWildcardDeletion()
    {
        var project = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "Fluxo",
            "Fluxo.csproj"));

        var legacyArtifacts = new[]
        {
            "Fluxo.exe",
            "Fluxo.dll",
            "Fluxo.deps.json",
            "Fluxo.runtimeconfig.json",
            "Fluxo.pdb",
        };

        Assert.Contains("LegacyPrimaryArtifactsToDelete", project);

        var buildCleanupTarget = GetTarget(project, "CleanupLegacyPrimaryArtifactsBeforeBuild");
        var publishCleanupTarget = GetTarget(project, "CleanupLegacyPrimaryArtifactsBeforePublish");

        Assert.Contains("BeforeTargets=\"Build\"", buildCleanupTarget);
        Assert.Contains("BeforeTargets=\"Publish\"", publishCleanupTarget);
        Assert.Contains("Condition=\"'$(DesignTimeBuild)' != 'true'\"", buildCleanupTarget);
        Assert.Contains("Condition=\"'$(DesignTimeBuild)' != 'true'\"", publishCleanupTarget);
        Assert.Contains("FLUXO_LEGACY_CLEANUP_DIR=$(TargetDir)", buildCleanupTarget);
        Assert.Contains("FLUXO_LEGACY_CLEANUP_DIR=$(PublishDir)", publishCleanupTarget);
        Assert.Contains("$cleanupDir = $env:FLUXO_LEGACY_CLEANUP_DIR", buildCleanupTarget);
        Assert.Contains("$cleanupDir = $env:FLUXO_LEGACY_CLEANUP_DIR", publishCleanupTarget);

        foreach (var artifact in legacyArtifacts)
        {
            Assert.Contains(artifact, project);
        }

        Assert.DoesNotContain("Include=\"$(TargetDir)Fluxo*\"", project);
        Assert.DoesNotContain("Include=\"$(PublishDir)Fluxo*\"", project);
        Assert.DoesNotContain("<LegacyPrimaryArtifactsToDelete>'Fluxo*", project);
        Assert.DoesNotContain("Remove-Item -LiteralPath '$(TargetDir)Fluxo*", project);
        Assert.DoesNotContain("Remove-Item -LiteralPath '$(PublishDir)Fluxo*", project);
        Assert.DoesNotContain("-LiteralPath '$(TargetDir)'", project);
        Assert.DoesNotContain("-LiteralPath '$(PublishDir)'", project);
        Assert.DoesNotContain("AfterTargets=\"Build\"", buildCleanupTarget);
        Assert.DoesNotContain("AfterTargets=\"Publish\"", publishCleanupTarget);
        Assert.DoesNotContain("Fluxo.Core.dll", project);
        Assert.DoesNotContain("Fluxo.Data.dll", project);
        Assert.DoesNotContain("Fluxo.Resources.dll", project);
        Assert.DoesNotContain("Fluxo.Services.dll", project);
    }

    [Fact]
    public void MsiHarvest_IncludesRootAndRecursiveAppOutputFiles()
    {
        var wxs = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "Fluxo.Installer.Msi",
            "ExampleComponents.wxs"));

        Assert.Contains("<Files Include=\"$(var.FluxoAppOutputDir)\\*\">", wxs);
        Assert.Contains("<Exclude Files=\"$(var.FluxoAppOutputDir)\\*.db\" />", wxs);
        Assert.Contains("<Files Include=\"$(var.FluxoAppOutputDir)\\**\">", wxs);
        Assert.Contains("<Exclude Files=\"$(var.FluxoAppOutputDir)\\*\" />", wxs);
        Assert.Contains("<Exclude Files=\"$(var.FluxoAppOutputDir)\\**\\*.db\" />", wxs);
    }

    [Fact]
    public void Dockerfile_UsesLowercaseExecutableEntrypoint()
    {
        var dockerfile = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "Dockerfile"));

        var finalNonEmptyLine = dockerfile
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .Last(line => !string.IsNullOrWhiteSpace(line));

        Assert.StartsWith("ENTRYPOINT [", finalNonEmptyLine);
        Assert.Contains("fluxo.exe", finalNonEmptyLine);
        Assert.DoesNotContain("Fluxo.exe", finalNonEmptyLine);
        Assert.Contains("COPY [\"Fluxo.Resources/Fluxo.Resources.csproj\", \"Fluxo.Resources/\"]", dockerfile);
    }

    private static string GetTarget(string project, string targetName)
    {
        var start = project.IndexOf($"<Target Name=\"{targetName}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Expected target '{targetName}' to exist.");

        var end = project.IndexOf("</Target>", start, StringComparison.Ordinal);
        Assert.True(end >= 0, $"Expected target '{targetName}' to have a closing tag.");

        return project[start..(end + "</Target>".Length)];
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
