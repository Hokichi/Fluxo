using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace Fluxo.Infrastructure;

internal static class AssemblyResolutionBootstrap
{
    private static readonly string[] ProbeFolders = ["libs", "vendor", "plugins"];

    [ModuleInitializer]
    internal static void Initialize()
    {
        AppDomain.CurrentDomain.AssemblyResolve += ResolveFromOrganizedOutputFolders;
        AssemblyLoadContext.Default.Resolving += ResolveLoadContextFromOrganizedOutputFolders;
    }

    private static Assembly? ResolveLoadContextFromOrganizedOutputFolders(
        AssemblyLoadContext loadContext,
        AssemblyName assemblyName)
    {
        var candidatePath = FindAssemblyPath(assemblyName.Name);
        return candidatePath is null ? null : loadContext.LoadFromAssemblyPath(candidatePath);
    }

    private static Assembly? ResolveFromOrganizedOutputFolders(object? _, ResolveEventArgs args)
    {
        var assemblyName = new AssemblyName(args.Name);
        var candidatePath = FindAssemblyPath(assemblyName.Name);
        return candidatePath is null ? null : Assembly.LoadFrom(candidatePath);
    }

    private static string? FindAssemblyPath(string? assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName))
            return null;

        var fileName = assemblyName + ".dll";
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;

        foreach (var folder in ProbeFolders)
        {
            var candidatePath = Path.Combine(baseDir, folder, fileName);
            if (File.Exists(candidatePath))
                return candidatePath;
        }

        return null;
    }
}
