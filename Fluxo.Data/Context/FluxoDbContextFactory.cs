using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Fluxo.Data.Context;

public sealed class FluxoDbContextFactory : IDesignTimeDbContextFactory<FluxoDbContext>
{
    public FluxoDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<FluxoDbContext>();
        optionsBuilder.UseSqlite(
            BuildConnectionString(),
            sqliteOptions => sqliteOptions.MigrationsAssembly("Fluxo"));

        return new FluxoDbContext(optionsBuilder.Options);
    }

    public static string BuildConnectionString()
    {
        var databasePath = GetDatabasePath();
        return $"Data Source={databasePath}";
    }

    public static string GetDatabasePath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return Path.Combine(appDataPath, "fluxo", "fluxo.db");
    }

    /// <summary>
    /// Ensures the directory for <see cref="GetDatabasePath"/> exists. SQLite does not create parent folders.
    /// </summary>
    public static void EnsureDatabaseDirectoryExists()
    {
        var databasePath = GetDatabasePath();
        var databaseDirectory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(databaseDirectory))
            Directory.CreateDirectory(databaseDirectory);
    }
}
