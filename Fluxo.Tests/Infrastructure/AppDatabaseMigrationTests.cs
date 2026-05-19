using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Data;
using Fluxo.Data.Context;
using Fluxo.Data.Operations;
using Fluxo.Data.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Fluxo.Tests.Infrastructure;

public sealed class AppDatabaseMigrationTests
{
    [Fact]
    public async Task MigrateDatabaseAsync_WhenDatabaseFileDoesNotExist_CreatesCurrentSchemaAndSeedsMigrationHistory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "fluxo-tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(tempDirectory, "fluxo.db");

        try
        {
            Directory.CreateDirectory(tempDirectory);
            using var serviceProvider = CreateServiceProvider(databasePath);
            var runner = serviceProvider.GetRequiredService<IDataOperationRunner>();

            await App.MigrateDatabaseAsync(runner, () => databasePath);

            using var verificationScope = serviceProvider.CreateScope();
            var dbContext = verificationScope.ServiceProvider.GetRequiredService<FluxoDbContext>();
            var migrations = dbContext.Database.GetMigrations().ToList();
            var appliedMigrations = (await dbContext.Database.GetAppliedMigrationsAsync()).ToList();

            Assert.True(File.Exists(databasePath));
            Assert.Equal(migrations.Count, appliedMigrations.Count);
            Assert.Empty(await dbContext.SpendingSources.ToListAsync());
            Assert.Empty(await dbContext.UserSettings.ToListAsync());
        }
        finally
        {
            SqliteConnection.ClearAllPools();

            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static ServiceProvider CreateServiceProvider(string databasePath)
    {
        var services = new ServiceCollection();

        services.AddSingleton(Substitute.For<ILogService>());
        services.AddDbContext<FluxoDbContext>(optionsBuilder =>
        {
            optionsBuilder.UseSqlite(
                $"Data Source={databasePath}",
                sqliteOptions => sqliteOptions.MigrationsAssembly("fluxo"));
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IExpenseRepository, ExpenseRepository>();
        services.AddScoped<IExpenseLogRepository, ExpenseLogRepository>();
        services.AddScoped<IIncomeLogRepository, IncomeLogRepository>();
        services.AddScoped<IExpenseTagRepository, ExpenseTagRepository>();
        services.AddScoped<ISavingGoalRepository, SavingGoalRepository>();
        services.AddScoped<ISpendingSourceRepository, SpendingSourceRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IUserSettingsRepository, UserSettingsRepository>();
        services.AddSingleton<IDataOperationScopeFactory, DataOperationScopeFactory>();
        services.AddSingleton<IDataOperationRunner, DataOperationRunner>();

        return services.BuildServiceProvider();
    }
}
