using System.Reflection;

namespace Fluxo.Services.Updates;

public static class AppVersionResolver
{
    public static string ResolveCurrentVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        var assemblyVersion = assembly.GetName().Version;

        return ResolveVersion(informationalVersion, assemblyVersion);
    }

    internal static string ResolveVersion(string? informationalVersion, Version? assemblyVersion)
    {
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            var metadataIndex = informationalVersion.IndexOf('+');
            return metadataIndex > 0
                ? informationalVersion[..metadataIndex]
                : informationalVersion;
        }

        if (assemblyVersion is null)
            return "Unknown";

        var build = assemblyVersion.Build >= 0 ? assemblyVersion.Build : 0;
        return $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{build}";
    }
}
