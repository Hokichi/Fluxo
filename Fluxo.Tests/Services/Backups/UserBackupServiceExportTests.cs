using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Services.Backups;
using NSubstitute;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace Fluxo.Tests.Services.Backups;

public sealed class UserBackupServiceExportTests
{
    private static readonly JsonSerializerOptions BackupJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    [Fact]
    public async Task BackupAsync_WhenTagsAreNotSelected_MapsExpensesToDataRestorationTag()
    {
        var appData = Substitute.For<IAppDataService>();
        appData.GetSpendingSourcesAsync(Arg.Any<CancellationToken>())
            .Returns([new SpendingSource { Id = 7, Name = "Wallet", SpendingSourceType = SpendingSourceType.Cash }]);
        appData.GetExpenseTagsAsync(Arg.Any<CancellationToken>())
            .Returns([new ExpenseTag { Id = 3, Name = "Food", HexCode = "#ffffff" }]);
        appData.GetExpensesAsync(Arg.Any<CancellationToken>())
            .Returns([new Expense { Id = 9, SpendingSourceId = 7, ExpenseTagId = 3, Name = "Lunch" }]);
        appData.GetExpenseLogsAsync(Arg.Any<CancellationToken>()).Returns([]);

        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        var service = new UserBackupService(appData);

        try
        {
            var result = await service.BackupAsync(new UserBackupSelection(new HashSet<DataManagementEntityKind>
            {
                DataManagementEntityKind.Expenses,
                DataManagementEntityKind.SpendingSources
            }), tempFile);

            Assert.True(result.IsSuccess);
            var json = await File.ReadAllTextAsync(tempFile);
            var document = JsonSerializer.Deserialize<FluxoUserBackupDocument>(json, BackupJsonOptions);
            Assert.NotNull(document);

            var dataRestorationTag = Assert.Single(document.Entities.Tags, tag => tag.Name == "Data Restoration");
            Assert.Equal("#e9c178", dataRestorationTag.HexCode);

            var expense = Assert.Single(document.Entities.Expenses);
            Assert.Equal(dataRestorationTag.BackupId, expense.ExpenseTagBackupId);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task BackupAsync_WhenTagsSelected_IncludesSpendingLimit()
    {
        var appData = Substitute.For<IAppDataService>();
        appData.GetExpenseTagsAsync(Arg.Any<CancellationToken>())
            .Returns([new ExpenseTag { Id = 3, Name = "Food", HexCode = "#ffffff", SpendingLimit = 250m }]);

        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        var service = new UserBackupService(appData);

        try
        {
            var result = await service.BackupAsync(new UserBackupSelection(new HashSet<DataManagementEntityKind>
            {
                DataManagementEntityKind.Tags
            }), tempFile);

            Assert.True(result.IsSuccess, result.ErrorMessage);
            var json = await File.ReadAllTextAsync(tempFile);
            var document = JsonSerializer.Deserialize<FluxoUserBackupDocument>(json, BackupJsonOptions);
            Assert.NotNull(document);

            var tag = Assert.Single(document.Entities.Tags);
            Assert.Equal(250m, tag.SpendingLimit);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task BackupAsync_WhenDeductSourceAppearsAfterDependent_MapsDeductSourceByBackupId()
    {
        var appData = Substitute.For<IAppDataService>();
        appData.GetSpendingSourcesAsync(Arg.Any<CancellationToken>())
            .Returns([
                new SpendingSource
                {
                    Id = 11,
                    Name = "Card A",
                    SpendingSourceType = SpendingSourceType.Credit,
                    DeductSource = 12
                },
                new SpendingSource
                {
                    Id = 12,
                    Name = "Wallet",
                    SpendingSourceType = SpendingSourceType.Cash
                }
            ]);

        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        var service = new UserBackupService(appData);

        try
        {
            var result = await service.BackupAsync(new UserBackupSelection(new HashSet<DataManagementEntityKind>
            {
                DataManagementEntityKind.SpendingSources
            }), tempFile);

            Assert.True(result.IsSuccess);
            var json = await File.ReadAllTextAsync(tempFile);
            var document = JsonSerializer.Deserialize<FluxoUserBackupDocument>(json, BackupJsonOptions);
            Assert.NotNull(document);

            var dependentSource = Assert.Single(document.Entities.SpendingSources, source => source.Name == "Card A");
            var deductSource = Assert.Single(document.Entities.SpendingSources, source => source.Name == "Wallet");

            Assert.Equal(deductSource.BackupId, dependentSource.DeductSourceBackupId);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task BackupAsync_WhenUserSettingsSelected_ExcludesLegacyBudgetAllocationSettings()
    {
        var appData = Substitute.For<IAppDataService>();
        appData.GetUserSettingsAsync(Arg.Any<CancellationToken>())
            .Returns([
                new UserSettings { Name = "NeedsThreshold", Value = "40" },
                new UserSettings { Name = "WantsThreshold", Value = "40" },
                new UserSettings { Name = "InvestThreshold", Value = "20" },
                new UserSettings { Name = "AllocationPeriod", Value = "Yearly" },
                new UserSettings { Name = "AllocationLimit", Value = "1000" },
                new UserSettings { Name = "RolloverPolicy", Value = "Pooled" },
                new UserSettings { Name = "OverspendPolicy", Value = "SoftDebt" },
                new UserSettings { Name = "NeedsDebt", Value = "5" },
                new UserSettings { Name = "WantsDebt", Value = "6" },
                new UserSettings { Name = "InvestDebt", Value = "7" },
                new UserSettings { Name = "PreferredDisplayName", Value = "Alex" }
            ]);

        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        var service = new UserBackupService(appData);

        try
        {
            var result = await service.BackupAsync(new UserBackupSelection(new HashSet<DataManagementEntityKind>
            {
                DataManagementEntityKind.UserSettings
            }), tempFile);

            Assert.True(result.IsSuccess, result.ErrorMessage);
            var json = await File.ReadAllTextAsync(tempFile);
            var document = JsonSerializer.Deserialize<FluxoUserBackupDocument>(json, BackupJsonOptions);
            Assert.NotNull(document);

            var setting = Assert.Single(document.Entities.UserSettings);
            Assert.Equal("PreferredDisplayName", setting.Name);
            Assert.Equal("Alex", setting.Value);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ReadManifestAsync_ReturnsIncludedEntityKinds()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(tempFile, """
        {
          "schemaVersion": 1,
          "createdAt": "2026-05-26T00:00:00Z",
          "includedEntities": [ "expenses", "spendingSources" ],
          "entities": {}
        }
        """);

        try
        {
            var service = new UserBackupService(Substitute.For<IAppDataService>());
            var manifest = await service.ReadManifestAsync(tempFile);

            Assert.Contains(DataManagementEntityKind.Expenses, manifest.IncludedEntities);
            Assert.Contains(DataManagementEntityKind.SpendingSources, manifest.IncludedEntities);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ReadManifestAsync_WhenIncludedEntitiesIsNull_ThrowsInvalidDataException()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(tempFile, """
        {
          "schemaVersion": 1,
          "createdAt": "2026-05-26T00:00:00Z",
          "includedEntities": null,
          "entities": {}
        }
        """);

        try
        {
            var service = new UserBackupService(Substitute.For<IAppDataService>());
            var exception = await Assert.ThrowsAsync<InvalidDataException>(() => service.ReadManifestAsync(tempFile));
            Assert.Equal("Backup file is missing includedEntities.", exception.Message);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
