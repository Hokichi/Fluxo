using Microsoft.Win32;

namespace Fluxo.Installer.Services;

public static class InstalledVersionRegistryReader
{
    public const string InstalledVersionSubKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\fluxo";
    public const string InstalledVersionValueName = "InstalledVersion";

    public static string? ReadInstalledVersion()
    {
        foreach (var registryView in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, registryView);
                var version = ReadInstalledVersion(baseKey);
                if (!string.IsNullOrWhiteSpace(version))
                {
                    return version;
                }
            }
            catch
            {
            }
        }

        return null;
    }

    public static string? ReadInstalledVersion(RegistryKey baseKey, string subKeyPath = InstalledVersionSubKeyPath)
    {
        try
        {
            using var fluxoKey = baseKey.OpenSubKey(subKeyPath);
            var installedVersion = fluxoKey?.GetValue(InstalledVersionValueName) as string;
            return string.IsNullOrWhiteSpace(installedVersion) ? null : installedVersion;
        }
        catch
        {
            return null;
        }
    }
}
