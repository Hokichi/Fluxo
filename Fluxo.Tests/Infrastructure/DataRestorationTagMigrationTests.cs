using Fluxo.Core.Interfaces;
using Fluxo.Core.Constants;
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

public sealed class DataRestorationTagMigrationTests
{
    private const string SeedDataRestorationMigrationId = "20260526050040_SeedDataRestorationTag";
    private const string SeedBudgetReconciliationMigrationId = "20260615023523_SeedBudgetReconciliationTag";

    [Fact]
    public async Task MigrateDatabaseAsync_SeedsDataRestorationTag()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "fluxo-tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(tempDirectory, "fluxo.db");

        try
        {
            Directory.CreateDirectory(tempDirectory);
            using var serviceProvider = CreateServiceProvider(databasePath);
            var runner = serviceProvider.GetRequiredService<IDataOperationRunner>();

            await App.MigrateDatabaseAsync(runner, () => databasePath);
            await InsertNonSystemDataRestorationTagAsync(databasePath);
            await RemoveMigrationHistoryEntryAsync(databasePath);
            await App.MigrateDatabaseAsync(runner, () => databasePath);

            var options = new DbContextOptionsBuilder<FluxoDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;
            await using var dbContext = new FluxoDbContext(options);

            var tags = await dbContext.ExpenseTags
                .Where(tag => tag.Name == "Data Restoration")
                .ToListAsync();

            var systemTag = tags.SingleOrDefault(tag => tag.IsSystemTag);
            Assert.NotNull(systemTag);
            Assert.Equal("#e9c178", systemTag.HexCode);

            var customTag = tags.SingleOrDefault(tag => !tag.IsSystemTag);
            Assert.NotNull(customTag);
            Assert.Equal("#123456", customTag.HexCode);

            Assert.Equal(2, tags.Count);
        }
        finally
        {
            SqliteConnection.ClearAllPools();

            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task MigrateDatabaseAsync_SeedsBudgetReconciliationTag()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "fluxo-tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(tempDirectory, "fluxo.db");

        try
        {
            Directory.CreateDirectory(tempDirectory);
            using var serviceProvider = CreateServiceProvider(databasePath);
            var runner = serviceProvider.GetRequiredService<IDataOperationRunner>();

            await App.MigrateDatabaseAsync(runner, () => databasePath);
            await InsertNonSystemTagAsync(
                databasePath,
                SystemExpenseTags.BudgetReconciliationName,
                "#123456");
            await RemoveMigrationHistoryEntryAsync(databasePath, SeedBudgetReconciliationMigrationId);
            await App.MigrateDatabaseAsync(runner, () => databasePath);

            var options = new DbContextOptionsBuilder<FluxoDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;
            await using var dbContext = new FluxoDbContext(options);

            var tags = await dbContext.ExpenseTags
                .Where(tag => tag.Name == SystemExpenseTags.BudgetReconciliationName)
                .ToListAsync();

            var systemTag = tags.SingleOrDefault(tag => tag.IsSystemTag);
            Assert.NotNull(systemTag);
            Assert.Equal(SystemExpenseTags.BudgetReconciliationHexCode, systemTag.HexCode);

            var customTag = tags.SingleOrDefault(tag => !tag.IsSystemTag);
            Assert.NotNull(customTag);
            Assert.Equal("#123456", customTag.HexCode);

            Assert.Equal(2, tags.Count);
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
        services.AddScoped<IRecurringTransactionRepository, RecurringTransactionRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IUserSettingsRepository, UserSettingsRepository>();
        services.AddSingleton<IDataOperationScopeFactory, DataOperationScopeFactory>();
        services.AddSingleton<IDataOperationRunner, DataOperationRunner>();

        return services.BuildServiceProvider();
    }

    private static Task RemoveMigrationHistoryEntryAsync(string databasePath)
    {
        return RemoveMigrationHistoryEntryAsync(databasePath, SeedDataRestorationMigrationId);
    }

    private static async Task RemoveMigrationHistoryEntryAsync(string databasePath, string migrationId)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
                              DELETE FROM "__EFMigrationsHistory"
                              WHERE "MigrationId" = $migrationId;
                              """;
        command.Parameters.AddWithValue("$migrationId", migrationId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertNonSystemDataRestorationTagAsync(string databasePath)
    {
        await InsertNonSystemTagAsync(databasePath, "Data Restoration", "#123456");
    }

    private static async Task InsertNonSystemTagAsync(string databasePath, string name, string hexCode)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
                              INSERT INTO ExpenseTags (Name, HexCode, IsSystemTag)
                              VALUES ($name, $hexCode, 0);
                              """;
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$hexCode", hexCode);
        await command.ExecuteNonQueryAsync();
    }
}
