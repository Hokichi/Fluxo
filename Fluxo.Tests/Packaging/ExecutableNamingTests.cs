using System;
using System.IO;
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

        Assert.Contains("<AssemblyName>fluxo</AssemblyName>", project);
        Assert.Contains("<RootNamespace>Fluxo</RootNamespace>", project);
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
        Assert.Contains("<Files Include=\"$(var.FluxoAppOutputDir)\\**\\*\">", wxs);
        Assert.Contains("<Exclude Files=\"$(var.FluxoAppOutputDir)\\**\\*.db\" />", wxs);
    }

    [Fact]
    public void Dockerfile_UsesLowercaseExecutableEntrypoint()
    {
        var dockerfile = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "Dockerfile"));

        Assert.Contains("ENTRYPOINT [\"C:\\\\app\\\\fluxo.exe\"]", dockerfile);
        Assert.DoesNotContain("ENTRYPOINT [\"C:\\\\app\\\\Fluxo.exe\"]", dockerfile);
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