using Microsoft.Win32;

namespace Fluxo.Installer.Services;

public sealed record DotNetRuntimeOwnershipMarker(
    string Version,
    string Rid,
    string InstallerUrl,
    bool InstalledByFluxo);

public interface IDotNetRuntimeOwnershipStore
{
    DotNetRuntimeOwnershipMarker? Read();
    void Save(DotNetRuntimeOwnershipMarker marker);
    void Clear();
}

public sealed class DotNetRuntimeOwnershipStore : IDotNetRuntimeOwnershipStore
{
    public const string RuntimeMarkerSubKeyPath = @"SOFTWARE\fluxo\Runtime";
    private const string VersionValueName = "Version";
    private const string RidValueName = "Rid";
    private const string InstallerUrlValueName = "InstallerUrl";
    private const string InstalledByFluxoValueName = "InstalledByFluxo";

    private readonly Func<string, string?> readValue;
    private readonly Action<string, string> writeValue;
    private readonly Action deleteKey;

    public DotNetRuntimeOwnershipStore(
        Func<string, string?>? readValue = null,
        Action<string, string>? writeValue = null,
        Action? deleteKey = null)
    {
        this.readValue = readValue ?? ReadRegistryValue;
        this.writeValue = writeValue ?? WriteRegistryValue;
        this.deleteKey = deleteKey ?? DeleteRegistryKey;
    }

    public DotNetRuntimeOwnershipMarker? Read()
    {
        if (!bool.TryParse(readValue(InstalledByFluxoValueName), out var installedByFluxo) || !installedByFluxo)
        {
            return null;
        }

        var version = readValue(VersionValueName);
        var rid = readValue(RidValueName);
        var installerUrl = readValue(InstallerUrlValueName);
        if (string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(rid) || string.IsNullOrWhiteSpace(installerUrl))
        {
            return null;
        }

        return new DotNetRuntimeOwnershipMarker(version, rid, installerUrl, installedByFluxo);
    }

    public void Save(DotNetRuntimeOwnershipMarker marker)
    {
        if (!marker.InstalledByFluxo)
        {
            throw new ArgumentException("Runtime ownership marker must represent a runtime installed by Fluxo.", nameof(marker));
        }

        ThrowIfBlank(marker.Version, nameof(marker.Version));
        ThrowIfBlank(marker.Rid, nameof(marker.Rid));
        ThrowIfBlank(marker.InstallerUrl, nameof(marker.InstallerUrl));

        writeValue(InstalledByFluxoValueName, marker.InstalledByFluxo.ToString());
        writeValue(VersionValueName, marker.Version);
        writeValue(RidValueName, marker.Rid);
        writeValue(InstallerUrlValueName, marker.InstallerUrl);
    }

    public void Clear() => deleteKey();

    private static string? ReadRegistryValue(string name)
    {
        using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = baseKey.OpenSubKey(RuntimeMarkerSubKeyPath);
        return key?.GetValue(name) as string;
    }

    private static void WriteRegistryValue(string name, string value)
    {
        using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = baseKey.CreateSubKey(RuntimeMarkerSubKeyPath, writable: true);
        key.SetValue(name, value, RegistryValueKind.String);
    }

    private static void DeleteRegistryKey()
    {
        using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        baseKey.DeleteSubKeyTree(RuntimeMarkerSubKeyPath, throwOnMissingSubKey: false);
    }

    private static void ThrowIfBlank(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Runtime ownership marker values cannot be blank.", parameterName);
        }
    }
}
