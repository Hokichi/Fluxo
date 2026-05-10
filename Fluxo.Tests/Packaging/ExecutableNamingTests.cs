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

        Assert.Equal("ENTRYPOINT [\"C:\\\\app\\\\fluxo.exe\"]", finalNonEmptyLine);
        Assert.DoesNotContain("ENTRYPOINT [\"C:\\\\app\\\\Fluxo.exe\"]", dockerfile);
        Assert.Contains("COPY [\"Fluxo.Resources/Fluxo.Resources.csproj\", \"Fluxo.Resources/\"]", dockerfile);
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
