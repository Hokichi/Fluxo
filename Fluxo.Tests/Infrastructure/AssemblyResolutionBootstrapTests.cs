using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Infrastructure;

public sealed class AssemblyResolutionBootstrapTests
{
    [Fact]
    public void Bootstrap_RegistersNetCoreAssemblyLoadContextResolverForOrganizedFolders()
    {
        var source = File.ReadAllText(RepositoryPaths.File(
            "Fluxo",
            "Infrastructure",
            "AssemblyResolutionBootstrap.cs"));

        Assert.Contains("using System.Runtime.Loader;", source);
        Assert.Contains("AssemblyLoadContext.Default.Resolving +=", source);
        Assert.Contains("ResolveLoadContextFromOrganizedOutputFolders", source);
        Assert.Contains("\"libs\"", source);
        Assert.Contains("\"vendor\"", source);
    }
}
