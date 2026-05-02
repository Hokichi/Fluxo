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
        return Path.Combine(AppContext.BaseDirectory, "fluxo.db");
    }
}
