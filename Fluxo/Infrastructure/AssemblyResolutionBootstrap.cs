using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Fluxo.Infrastructure;

internal static class AssemblyResolutionBootstrap
{
    private static readonly string[] ProbeFolders = ["libs", "vendor", "plugins"];

    [ModuleInitializer]
    internal static void Initialize()
    {
        AppDomain.CurrentDomain.AssemblyResolve += ResolveFromOrganizedOutputFolders;
    }

    private static Assembly? ResolveFromOrganizedOutputFolders(object? _, ResolveEventArgs args)
    {
        var assemblyName = new AssemblyName(args.Name).Name;
        if (string.IsNullOrWhiteSpace(assemblyName))
            return null;

        var fileName = assemblyName + ".dll";
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;

        foreach (var folder in ProbeFolders)
        {
            var candidatePath = Path.Combine(baseDir, folder, fileName);
            if (File.Exists(candidatePath))
                return Assembly.LoadFrom(candidatePath);
        }

        return null;
    }
}
