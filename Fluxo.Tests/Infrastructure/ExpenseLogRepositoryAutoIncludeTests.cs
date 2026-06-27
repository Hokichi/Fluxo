using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Data.Context;
using Fluxo.Data.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fluxo.Tests.Infrastructure;

public sealed class ExpenseLogRepositoryAutoIncludeTests
{
    [Fact]
    public async Task GetMarkedForDeletionAsync_WithParentLogRelationship_DoesNotTriggerAutoIncludeCycle()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<FluxoDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setupContext = new FluxoDbContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync();

            var account = new Account
            {
                Name = "Checking",
                AccountType = AccountType.Checking,
                Balance = 100m,
                AccountLimit = 0m,
                SpentAmount = 0m,
                DeductSource = 1,
                IsEnabled = true,
                PinnedOnUI = true
            };

            var tag = new Tag
            {
                Name = "General",
                HexCode = "#FFFFFF"
            };

            var expense = new Expense
            {
                Name = "Groceries",
                Amount = 30m,
                Tag = tag,
                Account = account
            };

            var parentLog = new ExpenseLog
            {
                Expense = expense,
                Account = account,
                Amount = 30m,
                DeductedOn = new DateTime(2026, 6, 18),
                Notes = string.Empty,
                IsForDeletion = true
            };

            var childLog = new ExpenseLog
            {
                Expense = expense,
                Account = account,
                ParentLog = parentLog,
                Amount = 10m,
                DeductedOn = new DateTime(2026, 6, 18),
                Notes = string.Empty,
                IsForDeletion = true
            };

            await setupContext.ExpenseLogs.AddRangeAsync(parentLog, childLog);
            await setupContext.SaveChangesAsync();
        }

        await using var queryContext = new FluxoDbContext(options);
        var repository = new ExpenseLogRepository(queryContext);

        var markedLogs = await repository.GetMarkedForDeletionAsync();

        Assert.Equal(2, markedLogs.Count);
        Assert.Contains(markedLogs, log => log.ParentLogId is null);
        Assert.Contains(markedLogs, log => log.ParentLogId is not null);
    }
}
