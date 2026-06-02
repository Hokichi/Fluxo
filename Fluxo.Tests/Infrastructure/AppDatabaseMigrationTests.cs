using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Core.Enums;
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
    private const string MoveRecurringPeriodMigrationId = "20260520090000_MoveRecurringPeriodToRecurringTransactions";
    private const string AddBudgetAllocationMigrationId = "20260602084415_AddBudgetAllocation";

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

            var budgetAllocation = await dbContext.BudgetAllocation.SingleAsync();
            Assert.Equal(50, budgetAllocation.NeedsThreshold);
            Assert.Equal(30, budgetAllocation.WantsThreshold);
            Assert.Equal(20, budgetAllocation.InvestThreshold);
            Assert.Equal(AllocationPeriod.Monthly, budgetAllocation.AllocationPeriod);
            Assert.Equal(0m, budgetAllocation.AllocationLimit);
            Assert.Equal(RolloverPolicy.None, budgetAllocation.RolloverPolicy);
            Assert.Equal(OverspendPolicy.Ignore, budgetAllocation.OverspendPolicy);

            var movedKeys = new[]
            {
                "NeedsThreshold",
                "WantsThreshold",
                "InvestThreshold",
                "AllocationPeriod"
            };
            Assert.Empty(await dbContext.UserSettings
                .Where(setting => movedKeys.Contains(setting.Name))
                .ToListAsync());
        }
        finally
        {
            SqliteConnection.ClearAllPools();

            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task MigrateDatabaseAsync_WhenDatabaseHasOldBudgetUserSettings_MovesBudgetValuesToBudgetAllocation()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "fluxo-tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(tempDirectory, "fluxo.db");

        try
        {
            Directory.CreateDirectory(tempDirectory);
            using var serviceProvider = CreateServiceProvider(databasePath);
            await CreateOldBudgetUserSettingsDatabaseAsync(databasePath, serviceProvider);
            var runner = serviceProvider.GetRequiredService<IDataOperationRunner>();

            await App.MigrateDatabaseAsync(runner, () => databasePath);

            using var verificationScope = serviceProvider.CreateScope();
            var dbContext = verificationScope.ServiceProvider.GetRequiredService<FluxoDbContext>();

            var budgetAllocation = await dbContext.BudgetAllocation.SingleAsync();
            Assert.Equal(45, budgetAllocation.NeedsThreshold);
            Assert.Equal(35, budgetAllocation.WantsThreshold);
            Assert.Equal(20, budgetAllocation.InvestThreshold);
            Assert.Equal(AllocationPeriod.Quarterly, budgetAllocation.AllocationPeriod);
            Assert.Equal(0m, budgetAllocation.AllocationLimit);
            Assert.Equal(RolloverPolicy.None, budgetAllocation.RolloverPolicy);
            Assert.Equal(OverspendPolicy.Ignore, budgetAllocation.OverspendPolicy);

            var movedKeys = new[]
            {
                "NeedsThreshold",
                "WantsThreshold",
                "InvestThreshold",
                "AllocationPeriod"
            };
            Assert.Empty(await dbContext.UserSettings
                .Where(setting => movedKeys.Contains(setting.Name))
                .ToListAsync());
        }
        finally
        {
            SqliteConnection.ClearAllPools();

            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task MigrateDatabaseAsync_WhenDatabaseHasPreviousRecurringSchema_AppliesRecurringPeriodMove()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "fluxo-tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(tempDirectory, "fluxo.db");

        try
        {
            Directory.CreateDirectory(tempDirectory);
            using var serviceProvider = CreateServiceProvider(databasePath);
            await CreatePreviousRecurringSchemaDatabaseAsync(databasePath, serviceProvider);
            var runner = serviceProvider.GetRequiredService<IDataOperationRunner>();

            await App.MigrateDatabaseAsync(runner, () => databasePath);

            await using var connection = new SqliteConnection($"Data Source={databasePath}");
            await connection.OpenAsync();

            Assert.False(await ColumnExistsAsync(connection, "SavingGoals", "RecurringPeriod"));
            Assert.False(await ColumnExistsAsync(connection, "RecurringTransactions", "RecurringDate"));
            Assert.True(await ColumnExistsAsync(connection, "RecurringTransactions", "RecurringTime"));
            Assert.True(await ColumnExistsAsync(connection, "RecurringTransactions", "RecurringPeriod"));
            Assert.Contains(
                MoveRecurringPeriodMigrationId,
                await ReadMigrationHistoryAsync(connection));
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
        services.AddScoped<IBudgetAllocationRepository, BudgetAllocationRepository>();
        services.AddSingleton<IDataOperationScopeFactory, DataOperationScopeFactory>();
        services.AddSingleton<IDataOperationRunner, DataOperationRunner>();

        return services.BuildServiceProvider();
    }

    private static async Task CreateOldBudgetUserSettingsDatabaseAsync(
        string databasePath,
        IServiceProvider serviceProvider)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();

        await ExecuteNonQueryAsync(
            connection,
            """
            CREATE TABLE "UserSettings" (
                "Name" TEXT NOT NULL CONSTRAINT "PK_UserSettings" PRIMARY KEY,
                "Value" TEXT NOT NULL
            );

            INSERT INTO "UserSettings" ("Name", "Value")
            VALUES
                ('NeedsThreshold', '45'),
                ('WantsThreshold', '35'),
                ('InvestThreshold', '20'),
                ('AllocationPeriod', 'Quarterly');

            CREATE TABLE "__EFMigrationsHistory" (
                "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                "ProductVersion" TEXT NOT NULL
            );
            """);

        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FluxoDbContext>();
        var migrationsToSeed = dbContext.Database.GetMigrations()
            .Where(migrationId => !string.Equals(
                migrationId,
                AddBudgetAllocationMigrationId,
                StringComparison.Ordinal))
            .ToList();

        foreach (var migrationId in migrationsToSeed)
        {
            await ExecuteNonQueryAsync(
                connection,
                """
                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                VALUES ($migrationId, '10.0.5');
                """,
                ("$migrationId", migrationId));
        }
    }

    private static async Task CreatePreviousRecurringSchemaDatabaseAsync(
        string databasePath,
        IServiceProvider serviceProvider)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();

        await ExecuteNonQueryAsync(
            connection,
            """
            CREATE TABLE "SavingGoals" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_SavingGoals" PRIMARY KEY AUTOINCREMENT,
                "CreatedOn" TEXT NOT NULL,
                "CurrentAmount" NUMERIC NOT NULL,
                "Name" TEXT NOT NULL,
                "RecurringPeriod" INTEGER NOT NULL,
                "SavingEndDate" TEXT NULL,
                "TargetAmount" NUMERIC NOT NULL
            );

            CREATE TABLE "RecurringTransactions" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_RecurringTransactions" PRIMARY KEY AUTOINCREMENT,
                "Name" TEXT NOT NULL,
                "Amount" NUMERIC NOT NULL,
                "RecurringDate" INTEGER NOT NULL,
                "Type" INTEGER NOT NULL,
                "SourceId" INTEGER NOT NULL,
                "TagId" INTEGER NULL,
                "GoalId" INTEGER NULL,
                "IsEnabled" INTEGER NOT NULL
            );

            CREATE TABLE "__EFMigrationsHistory" (
                "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                "ProductVersion" TEXT NOT NULL
            );
            """);

        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FluxoDbContext>();
        var migrationsToSeed = dbContext.Database.GetMigrations()
            .Where(migrationId => !string.Equals(
                migrationId,
                MoveRecurringPeriodMigrationId,
                StringComparison.Ordinal))
            .ToList();

        foreach (var migrationId in migrationsToSeed)
        {
            await ExecuteNonQueryAsync(
                connection,
                """
                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                VALUES ($migrationId, '10.0.5');
                """,
                ("$migrationId", migrationId));
        }
    }

    private static async Task<bool> ColumnExistsAsync(
        SqliteConnection connection,
        string tableName,
        string columnName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{tableName}\");";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var name = reader.GetString(1);
            if (string.Equals(name, columnName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static async Task<IReadOnlyList<string>> ReadMigrationHistoryAsync(SqliteConnection connection)
    {
        var migrations = new List<string>();

        await using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT "MigrationId"
                              FROM "__EFMigrationsHistory"
                              ORDER BY "MigrationId";
                              """;

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            migrations.Add(reader.GetString(0));

        return migrations;
    }

    private static async Task ExecuteNonQueryAsync(
        SqliteConnection connection,
        string commandText,
        (string Name, object Value)? parameter = null)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;

        if (parameter is { } value)
            command.Parameters.AddWithValue(value.Name, value.Value);

        await command.ExecuteNonQueryAsync();
    }
}
