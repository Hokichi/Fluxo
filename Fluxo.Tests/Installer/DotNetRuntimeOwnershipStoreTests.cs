using Fluxo.Installer.Services;
using Xunit;

namespace Fluxo.Tests.Installer;

public sealed class DotNetRuntimeOwnershipStoreTests
{
    [Fact]
    public void SaveAndRead_RoundTripsMarker()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var store = new DotNetRuntimeOwnershipStore(
            readValue: name => values.TryGetValue(name, out var value) ? value : null,
            writeValue: (name, value) => values[name] = value,
            deleteKey: () => values.Clear());

        var marker = new DotNetRuntimeOwnershipMarker(
            Version: "10.0.8",
            Rid: "win-x64",
            InstallerUrl: "https://example.test/runtime.exe",
            InstalledByFluxo: true);

        store.Save(marker);

        var result = store.Read();

        Assert.NotNull(result);
        Assert.Equal(marker, result);
    }

    [Fact]
    public void Read_ReturnsNull_WhenInstalledByFluxoMissing()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Version"] = "10.0.8",
            ["Rid"] = "win-x64",
            ["InstallerUrl"] = "https://example.test/runtime.exe",
        };
        var store = new DotNetRuntimeOwnershipStore(
            readValue: name => values.TryGetValue(name, out var value) ? value : null,
            writeValue: (name, value) => values[name] = value,
            deleteKey: () => values.Clear());

        Assert.Null(store.Read());
    }

    [Fact]
    public void Clear_RemovesMarkerValues()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["InstalledByFluxo"] = "true",
            ["Version"] = "10.0.8",
            ["Rid"] = "win-x64",
            ["InstallerUrl"] = "https://example.test/runtime.exe",
        };
        var store = new DotNetRuntimeOwnershipStore(
            readValue: name => values.TryGetValue(name, out var value) ? value : null,
            writeValue: (name, value) => values[name] = value,
            deleteKey: () => values.Clear());

        store.Clear();

        Assert.Empty(values);
    }
}
