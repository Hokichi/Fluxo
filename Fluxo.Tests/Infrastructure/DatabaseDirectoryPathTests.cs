using Xunit;

namespace Fluxo.Tests.Infrastructure;

public sealed class DatabaseDirectoryPathTests
{
    [Fact]
    public void DatabaseDirectoryPath_UsesPerUserLocalApplicationData()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var directoryPath = Fluxo.Data.Context.FluxoDbContextFactory.GetDatabaseDirectoryPath();

        Assert.Equal(Path.Combine(localAppData, "fluxo"), directoryPath);
    }

    [Fact]
    public void DatabasePath_IsTheFluxoDatabaseInLocalApplicationData()
    {
        var databasePath = Fluxo.Data.Context.FluxoDbContextFactory.GetDatabasePath();
        var directoryPath = Fluxo.Data.Context.FluxoDbContextFactory.GetDatabaseDirectoryPath();

        Assert.Equal(Path.Combine(directoryPath, "fluxo.db"), databasePath);
    }
}
