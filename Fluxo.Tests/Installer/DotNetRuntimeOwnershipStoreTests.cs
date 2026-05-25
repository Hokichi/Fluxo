using Fluxo.Installer.Services;
using Xunit;

namespace Fluxo.Tests.Installer;

public sealed class DotNetRuntimeOwnershipStoreTests
{
    [Fact]
    public void SaveAndRead_RoundTripsMarker()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var store = CreateStore(values);

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
        var store = CreateStore(values);

        Assert.Null(store.Read());
    }

    [Theory]
    [InlineData("false")]
    [InlineData("not-bool")]
    public void Read_ReturnsNull_WhenInstalledByFluxoIsNotTrue(string installedByFluxo)
    {
        var store = CreateStore(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["InstalledByFluxo"] = installedByFluxo,
            ["Version"] = "10.0.8",
            ["Rid"] = "win-x64",
            ["InstallerUrl"] = "https://example.test/runtime.exe",
        });

        Assert.Null(store.Read());
    }

    [Theory]
    [InlineData("Version")]
    [InlineData("Rid")]
    [InlineData("InstallerUrl")]
    public void Read_ReturnsNull_WhenRequiredMarkerFieldIsMissing(string missingField)
    {
        var values = CreateValidValues();
        values.Remove(missingField);
        var store = CreateStore(values);

        Assert.Null(store.Read());
    }

    [Theory]
    [InlineData("Version")]
    [InlineData("Rid")]
    [InlineData("InstallerUrl")]
    public void Read_ReturnsNull_WhenRequiredMarkerFieldIsBlank(string blankField)
    {
        var values = CreateValidValues();
        values[blankField] = " ";
        var store = CreateStore(values);

        Assert.Null(store.Read());
    }

    [Theory]
    [InlineData("", "win-x64", "https://example.test/runtime.exe", true)]
    [InlineData("10.0.8", "", "https://example.test/runtime.exe", true)]
    [InlineData("10.0.8", "win-x64", "", true)]
    [InlineData("10.0.8", "win-x64", "https://example.test/runtime.exe", false)]
    public void Save_Throws_WhenMarkerIsInvalid(
        string version,
        string rid,
        string installerUrl,
        bool installedByFluxo)
    {
        var store = CreateStore(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        var marker = new DotNetRuntimeOwnershipMarker(
            version,
            rid,
            installerUrl,
            installedByFluxo);

        Assert.Throws<ArgumentException>(() => store.Save(marker));
    }

    [Fact]
    public void Clear_RemovesMarkerValues()
    {
        var values = CreateValidValues();
        var store = CreateStore(values);

        store.Clear();

        Assert.Empty(values);
    }

    private static DotNetRuntimeOwnershipStore CreateStore(Dictionary<string, string> values) =>
        new(
            readValue: name => values.TryGetValue(name, out var value) ? value : null,
            writeValue: (name, value) => values[name] = value,
            deleteKey: () => values.Clear());

    private static Dictionary<string, string> CreateValidValues() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["InstalledByFluxo"] = "true",
            ["Version"] = "10.0.8",
            ["Rid"] = "win-x64",
            ["InstallerUrl"] = "https://example.test/runtime.exe",
        };
}
