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
    private const string AddBudgetAllocationPeriodStartMigrationId = "20260615051332_AddBudgetAllocationPeriodStart";
    private const string ConvertLegacyAccountType2ToCreditMigrationId = "20260619071800_ConvertLegacyAccountType2ToCredit";

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
            Assert.Empty(await dbContext.Accounts.ToListAsync());

            var budgetAllocation = await dbContext.BudgetAllocation.SingleAsync();
            Assert.Equal(50, budgetAllocation.NeedsThreshold);
            Assert.Equal(30, budgetAllocation.WantsThreshold);
            Assert.Equal(20, budgetAllocation.InvestThreshold);
            Assert.Equal(AllocationPeriod.Monthly, budgetAllocation.AllocationPeriod);
            Assert.Equal(1, budgetAllocation.PeriodStart);
            Assert.InRange(budgetAllocation.CurrentPeriodIndex, 1, 28);
            Assert.Equal(DateTime.MinValue, budgetAllocation.LastRolloverPeriodStart);
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
            Assert.Equal(1, budgetAllocation.PeriodStart);
            Assert.InRange(budgetAllocation.CurrentPeriodIndex, 1, 28);
            Assert.Equal(DateTime.MinValue, budgetAllocation.LastRolloverPeriodStart);
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

    [Fact]
    public async Task MigrateDatabaseAsync_WhenDatabaseHasLegacyAccountSchema_RenamesSchemaAndMigrationHistory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "fluxo-tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(tempDirectory, "fluxo.db");

        try
        {
            Directory.CreateDirectory(tempDirectory);
            using var serviceProvider = CreateServiceProvider(databasePath);
            await CreateLegacyAccountSchemaDatabaseAsync(databasePath, serviceProvider);
            var runner = serviceProvider.GetRequiredService<IDataOperationRunner>();

            await App.MigrateDatabaseAsync(runner, () => databasePath);

            await using var connection = new SqliteConnection($"Data Source={databasePath}");
            await connection.OpenAsync();

            var legacyEntityName = "Spending" + "Source";
            Assert.True(await TableExistsAsync(connection, "Accounts"));
            Assert.False(await TableExistsAsync(connection, legacyEntityName + "s"));
            Assert.True(await ColumnExistsAsync(connection, "Accounts", "AccountType"));
            Assert.False(await ColumnExistsAsync(connection, "Accounts", legacyEntityName + "Type"));
            Assert.True(await ColumnExistsAsync(connection, "Expenses", "AccountId"));
            Assert.False(await ColumnExistsAsync(connection, "Expenses", legacyEntityName + "Id"));
            Assert.True(await ColumnExistsAsync(connection, "ExpenseLogs", "AccountId"));
            Assert.True(await ColumnExistsAsync(connection, "IncomeLogs", "AccountId"));

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = """SELECT "Name", "AccountType" FROM "Accounts" WHERE "Id" = 7;""";
                await using var reader = await command.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                Assert.Equal("Wallet", reader.GetString(0));
                Assert.Equal((int)AccountType.Cash, reader.GetInt32(1));
            }

            var migrations = await ReadMigrationHistoryAsync(connection);
            Assert.Contains("20260401093922_AddShowOnUIToAccount", migrations);
            Assert.DoesNotContain("20260401093922_AddShowOnUITo" + legacyEntityName, migrations);
        }
        finally
        {
            SqliteConnection.ClearAllPools();

            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task MigrateDatabaseAsync_WhenDatabaseHasLegacyAccountType2_ConvertsItToCredit()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "fluxo-tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(tempDirectory, "fluxo.db");

        try
        {
            Directory.CreateDirectory(tempDirectory);
            using var serviceProvider = CreateServiceProvider(databasePath);
            await CreateDatabaseBeforeLegacyAccountType2ConversionAsync(databasePath, serviceProvider);
            var runner = serviceProvider.GetRequiredService<IDataOperationRunner>();

            await App.MigrateDatabaseAsync(runner, () => databasePath);

            await using var connection = new SqliteConnection($"Data Source={databasePath}");
            await connection.OpenAsync();

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = """SELECT "AccountType" FROM "Accounts" WHERE "Id" = 42;""";
                var value = Assert.IsType<long>(await command.ExecuteScalarAsync());
                Assert.Equal((int)AccountType.Credit, (int)value);
            }

            Assert.Contains(ConvertLegacyAccountType2ToCreditMigrationId, await ReadMigrationHistoryAsync(connection));
        }
        finally
        {
            SqliteConnection.ClearAllPools();

            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task MigrateDatabaseAsync_AddsIoUFlagColumns()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "fluxo-tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(tempDirectory, "fluxo.db");

        try
        {
            Directory.CreateDirectory(tempDirectory);
            using var serviceProvider = CreateServiceProvider(databasePath);
            var runner = serviceProvider.GetRequiredService<IDataOperationRunner>();

            await App.MigrateDatabaseAsync(runner, () => databasePath);

            await using var connection = new SqliteConnection($"Data Source={databasePath}");
            await connection.OpenAsync();

            Assert.True(await ColumnExistsAsync(connection, "Expenses", "IsIoU"));
            Assert.True(await ColumnExistsAsync(connection, "ExpenseLogs", "IsIoU"));
            Assert.True(await ColumnExistsAsync(connection, "IncomeLogs", "IsIoU"));
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
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<ISavingGoalRepository, SavingGoalRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
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
            .Where(migrationId =>
                !string.Equals(migrationId, AddBudgetAllocationMigrationId, StringComparison.Ordinal) &&
                !string.Equals(migrationId, AddBudgetAllocationPeriodStartMigrationId, StringComparison.Ordinal))
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

    private static async Task CreateDatabaseBeforeLegacyAccountType2ConversionAsync(
        string databasePath,
        IServiceProvider serviceProvider)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();

        await ExecuteNonQueryAsync(
            connection,
            """
            CREATE TABLE "Accounts" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_Accounts" PRIMARY KEY AUTOINCREMENT,
                "AccountLimit" NUMERIC NOT NULL,
                "AccountType" INTEGER NOT NULL,
                "Balance" NUMERIC NOT NULL,
                "DeductSource" INTEGER NULL,
                "InterestRate" REAL NULL,
                "IsEnabled" INTEGER NOT NULL,
                "IsForDeletion" INTEGER NOT NULL,
                "MaximumSpending" NUMERIC NOT NULL,
                "MinimumPayment" NUMERIC NULL,
                "MonthlyDueDate" INTEGER NULL,
                "Name" TEXT NOT NULL,
                "PinnedOnUI" INTEGER NOT NULL,
                "SpentAmount" NUMERIC NOT NULL
            );

            CREATE TABLE "__EFMigrationsHistory" (
                "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                "ProductVersion" TEXT NOT NULL
            );

            INSERT INTO "Accounts" (
                "Id",
                "AccountLimit",
                "AccountType",
                "Balance",
                "DeductSource",
                "InterestRate",
                "IsEnabled",
                "IsForDeletion",
                "MaximumSpending",
                "MinimumPayment",
                "MonthlyDueDate",
                "Name",
                "PinnedOnUI",
                "SpentAmount")
            VALUES (42, 1000, 2, 0, NULL, NULL, 1, 0, 1000, NULL, 15, 'Legacy Pay Later', 1, 120);
            """);

        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FluxoDbContext>();
        var migrationsToSeed = dbContext.Database.GetMigrations()
            .Where(migrationId => !string.Equals(
                migrationId,
                ConvertLegacyAccountType2ToCreditMigrationId,
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

    private static async Task CreateLegacyAccountSchemaDatabaseAsync(
        string databasePath,
        IServiceProvider serviceProvider)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();

        var legacyEntityName = "Spending" + "Source";
        var legacyTableName = legacyEntityName + "s";

        await ExecuteNonQueryAsync(
            connection,
            $"""
             CREATE TABLE "{legacyTableName}" (
                 "Id" INTEGER NOT NULL CONSTRAINT "PK_{legacyTableName}" PRIMARY KEY AUTOINCREMENT,
                 "Name" TEXT NOT NULL,
                 "{legacyEntityName}Type" INTEGER NOT NULL,
                 "AccountLimit" NUMERIC NOT NULL,
                 "MaximumSpending" NUMERIC NOT NULL,
                 "MinimumPayment" NUMERIC NULL,
                 "SpentAmount" NUMERIC NOT NULL,
                 "Balance" NUMERIC NOT NULL,
                 "MonthlyDueDate" INTEGER NULL,
                 "DeductSource" INTEGER NULL,
                 "IsEnabled" INTEGER NOT NULL,
                 "PinnedOnUI" INTEGER NOT NULL,
                 "InterestRate" REAL NOT NULL
             );

             CREATE TABLE "Expenses" (
                 "Id" INTEGER NOT NULL CONSTRAINT "PK_Expenses" PRIMARY KEY AUTOINCREMENT,
                 "Name" TEXT NOT NULL,
                 "Amount" NUMERIC NOT NULL,
                 "{legacyEntityName}Id" INTEGER NOT NULL,
                 "TagId" INTEGER NOT NULL,
                 "Date" TEXT NOT NULL,
                 "IsRecurring" INTEGER NOT NULL
             );

             CREATE TABLE "ExpenseLogs" (
                 "Id" INTEGER NOT NULL CONSTRAINT "PK_ExpenseLogs" PRIMARY KEY AUTOINCREMENT,
                 "ExpenseId" INTEGER NOT NULL,
                 "{legacyEntityName}Id" INTEGER NOT NULL,
                 "Amount" NUMERIC NOT NULL,
                 "Date" TEXT NOT NULL,
                 "IsForDeletion" INTEGER NOT NULL,
                 "IsPinned" INTEGER NOT NULL,
                 "Notes" TEXT NOT NULL
             );

             CREATE TABLE "IncomeLogs" (
                 "Id" INTEGER NOT NULL CONSTRAINT "PK_IncomeLogs" PRIMARY KEY AUTOINCREMENT,
                 "{legacyEntityName}Id" INTEGER NOT NULL,
                 "Name" TEXT NOT NULL,
                 "Amount" NUMERIC NOT NULL,
                 "Date" TEXT NOT NULL,
                 "IsForDeletion" INTEGER NOT NULL,
                 "IsPinned" INTEGER NOT NULL,
                 "Notes" TEXT NOT NULL
             );

             CREATE TABLE "__EFMigrationsHistory" (
                 "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                 "ProductVersion" TEXT NOT NULL
             );

             INSERT INTO "{legacyTableName}" (
                 "Id",
                 "Name",
                 "{legacyEntityName}Type",
                 "AccountLimit",
                 "MaximumSpending",
                 "SpentAmount",
                 "Balance",
                 "IsEnabled",
                 "PinnedOnUI",
                 "InterestRate")
             VALUES (7, 'Wallet', {(int)AccountType.Cash}, 0, 0, 0, 100, 1, 1, 0);
             """);

        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FluxoDbContext>();
        var migrationIds = dbContext.Database.GetMigrations()
            .Select(migrationId => migrationId switch
            {
                "20260401093922_AddShowOnUIToAccount" => "20260401093922_AddShowOnUITo" + legacyEntityName,
                "20260411142128_AddIsEnabledToAccount" => "20260411142128_AddIsEnabledTo" + legacyEntityName,
                "20260415014219_AddIsForDeletionToAccount" => "20260415014219_AddIsForDeletionTo" + legacyEntityName,
                "20260416153000_AddIconNameAndMonthlyDueDateToAccount" =>
                    "20260416153000_AddIconNameAndMonthlyDueDateTo" + legacyEntityName,
                _ => migrationId
            });

        foreach (var migrationId in migrationIds)
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

    private static async Task<bool> TableExistsAsync(
        SqliteConnection connection,
        string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT 1
                              FROM sqlite_master
                              WHERE type = 'table'
                                AND name = $name
                              LIMIT 1;
                              """;
        command.Parameters.AddWithValue("$name", tableName);

        return await command.ExecuteScalarAsync() is not null;
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
