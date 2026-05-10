using System.IO;
using System.Reflection;
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
        return Path.Combine(GetDatabaseDirectoryPath(), "fluxo.db");
    }

    public static string GetDatabaseDirectoryPath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return Path.Combine(appDataPath, "fluxo");
    }

    /// <summary>
    /// Ensures the directory for <see cref="GetDatabasePath"/> exists. SQLite does not create parent folders.
    /// </summary>
    public static void EnsureDatabaseDirectoryExists()
    {
        MachineWideDataDirectoryPreparer.Prepare(GetDatabaseDirectoryPath());
    }

    private static class MachineWideDataDirectoryPreparer
    {
        public static void Prepare(string directoryPath)
        {
            var preparerType = Type.GetType("Fluxo.Infrastructure.MachineWideDataDirectoryPreparer, fluxo");
            var prepareMethod = preparerType?.GetMethod("Prepare", BindingFlags.Public | BindingFlags.Static, [typeof(string)]);

            if (prepareMethod is not null)
            {
                prepareMethod.Invoke(obj: null, parameters: [directoryPath]);
                return;
            }

            Directory.CreateDirectory(directoryPath);
        }
    }
}
