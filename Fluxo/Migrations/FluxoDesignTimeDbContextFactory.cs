using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Fluxo.Migrations;

public sealed class FluxoDesignTimeDbContextFactory : IDesignTimeDbContextFactory<FluxoDbContext>
{
    public FluxoDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<FluxoDbContext>();
        optionsBuilder.UseSqlite(
            FluxoDbContextFactory.BuildConnectionString(),
            sqliteOptions => sqliteOptions.MigrationsAssembly("fluxo"));

        return new FluxoDbContext(optionsBuilder.Options);
    }
}
