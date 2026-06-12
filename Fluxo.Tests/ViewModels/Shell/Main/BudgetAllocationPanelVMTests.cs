using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Windows.Data;
using AutoMapper;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.DTO;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Resources.Resources.Messages;
using Fluxo.Services.History;
using Fluxo.Tests.TestDoubles;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Shell;
using Fluxo.ViewModels.Shell.Main;
using NSubstitute;
using Xunit;

namespace Fluxo.Tests.ViewModels.Shell.Main;

public class BudgetAllocationPanelVMTests
{
    [Fact]
    public void DateRangeMessage_UpdatesDisplayedBucketsForIncludedDates()
    {
        RunInSta(() =>
        {
            var messenger = new WeakReferenceMessenger();
            var vm = CreateVm(messenger, CreateExpenseLogs(), CreateTags(), CreateSpendingSources());
            vm.LoadAsync().GetAwaiter().GetResult();

            messenger.Send(new DateRangeSelectionChangedMessage(
                new DateTime(2026, 4, 10),
                new DateTime(2026, 4, 12)));

            Assert.Collection(
                GetItems(vm.Needs),
                item => Assert.Equal(1, item.Id));
            Assert.Collection(
                GetItems(vm.Wants),
                item => Assert.Equal(2, item.Id));
            Assert.Empty(GetItems(vm.Invest));
        });
    }

    [Fact]
    public void SelectedTag_FiltersVisibleItemsAcrossBuckets()
    {
        RunInSta(() =>
        {
            var messenger = new WeakReferenceMessenger();
            var vm = CreateVm(messenger, CreateExpenseLogs(), CreateTags(), CreateSpendingSources());
            vm.LoadAsync().GetAwaiter().GetResult();
            messenger.Send(new DateRangeSelectionChangedMessage(
                new DateTime(2026, 4, 10),
                new DateTime(2026, 4, 18)));

            vm.SelectedVisibleTag = vm.Tags.Single(tag => tag.Id == 1);

            Assert.Equal(1, vm.SelectedTag?.Id);
            Assert.Collection(
                GetItems(vm.Needs),
                item => Assert.Equal(1, item.Id));
            Assert.Empty(GetItems(vm.Wants));
            Assert.Collection(
                GetItems(vm.Invest),
                item => Assert.Equal(3, item.Id));
        });
    }

    [Fact]
    public void DateRangeMessage_OrdersTagsByUsageAndPushesSystemTagsToMore()
    {
        RunInSta(() =>
        {
            var messenger = new WeakReferenceMessenger();
            var vm = CreateVm(
                messenger,
                CreateExpenseLogsForUsageOrdering(),
                CreateTagsForUsageOrdering(),
                CreateSpendingSources());
            vm.LoadAsync().GetAwaiter().GetResult();

            messenger.Send(new DateRangeSelectionChangedMessage(
                new DateTime(2026, 4, 1),
                new DateTime(2026, 4, 30)));

            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, vm.Tags.Select(tag => tag.Id).ToArray());
            Assert.Equal(new[] { 6 }, vm.OtherTags.Select(tag => tag.Id).ToArray());
            Assert.True(vm.HasOtherTags);
        });
    }

    [Fact]
    public void SelectedOtherTag_PromotesTagToVisibleStartAndMovesFifthTagToMore()
    {
        RunInSta(() =>
        {
            var messenger = new WeakReferenceMessenger();
            var vm = CreateVm(
                messenger,
                CreateExpenseLogsForTagPromotion(),
                CreateTagsForTagPromotion(),
                CreateSpendingSources());
            vm.LoadAsync().GetAwaiter().GetResult();
            messenger.Send(new DateRangeSelectionChangedMessage(
                new DateTime(2026, 4, 1),
                new DateTime(2026, 4, 30)));

            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, vm.Tags.Select(tag => tag.Id).ToArray());
            Assert.Equal(new[] { 6 }, vm.OtherTags.Select(tag => tag.Id).ToArray());

            vm.SelectedOtherTag = vm.OtherTags.Single(tag => tag.Id == 6);

            Assert.Equal(6, vm.SelectedTag?.Id);
            Assert.Equal(new[] { 6, 1, 2, 3, 4 }, vm.Tags.Select(tag => tag.Id).ToArray());
            Assert.Equal(new[] { 5 }, vm.OtherTags.Select(tag => tag.Id).ToArray());
            Assert.Equal(6, vm.SelectedVisibleTag?.Id);
            Assert.Null(vm.SelectedOtherTag);
            Assert.True(vm.HasOtherTags);
        });
    }

    [Fact]
    public void DateRangeMessage_ResetsSelectedTagToAllWhenTagFallsOutOfRange()
    {
        RunInSta(() =>
        {
            var messenger = new WeakReferenceMessenger();
            var vm = CreateVm(messenger, CreateExpenseLogs(), CreateTags(), CreateSpendingSources());
            vm.LoadAsync().GetAwaiter().GetResult();

            messenger.Send(new DateRangeSelectionChangedMessage(
                new DateTime(2026, 4, 10),
                new DateTime(2026, 4, 18)));
            vm.SelectedVisibleTag = vm.Tags.Single(tag => tag.Id == 1);

            messenger.Send(new DateRangeSelectionChangedMessage(
                new DateTime(2026, 4, 12),
                new DateTime(2026, 4, 12)));

            Assert.Null(vm.SelectedTag);
            Assert.Null(vm.SelectedVisibleTag);
            Assert.Null(vm.SelectedOtherTag);
            Assert.False(vm.HasOtherTags);
            Assert.Collection(
                GetItems(vm.Wants),
                item => Assert.Equal(2, item.Id));
        });
    }

    [Fact]
    public void LoadAsync_UsesTypedBudgetAllocation()
    {
        RunInSta(() =>
        {
            var messenger = new WeakReferenceMessenger();
            var vm = CreateVm(
                messenger,
                CreateExpenseLogs(),
                CreateTags(),
                CreateSpendingSources(),
                budgetAllocation: new BudgetAllocation
                {
                    NeedsThreshold = 45,
                    WantsThreshold = 35,
                    InvestThreshold = 20
                });

            vm.LoadAsync().GetAwaiter().GetResult();

            Assert.Equal(45, vm.NeedsAllocationPercentage);
            Assert.Equal(35, vm.WantsAllocationPercentage);
            Assert.Equal(20, vm.InvestAllocationPercentage);
            Assert.Equal(900m, vm.NeedsAvailable);
            Assert.Equal(700m, vm.WantsAvailable);
            Assert.Equal(400m, vm.InvestAvailable);
        });
    }

    [Fact]
    public void LoadAsync_ExposesAllocationPercentagesForUiFromTypedAllocation()
    {
        RunInSta(() =>
        {
            var messenger = new WeakReferenceMessenger();
            var vm = CreateVm(
                messenger,
                CreateExpenseLogs(),
                CreateTags(),
                CreateSpendingSources(),
                budgetAllocation: new BudgetAllocation
                {
                    NeedsThreshold = 40,
                    WantsThreshold = 35,
                    InvestThreshold = 25
                });

            vm.LoadAsync().GetAwaiter().GetResult();

            Assert.Equal(40, vm.NeedsAllocationPercentage);
            Assert.Equal(35, vm.WantsAllocationPercentage);
            Assert.Equal(25, vm.InvestAllocationPercentage);
        });
    }

    [Fact]
    public void LoadAsync_FallsBackToDefaultBudgetThresholdsWhenSettingsInvalid()
    {
        RunInSta(() =>
        {
            var messenger = new WeakReferenceMessenger();
            var vm = CreateVm(
                messenger,
                CreateExpenseLogs(),
                CreateTags(),
                CreateSpendingSources(),
                settings:
                [
                    new UserSettings { Name = "NeedsThreshold", Value = "invalid" }
                ]);

            vm.LoadAsync().GetAwaiter().GetResult();

            Assert.Equal(1000m, vm.NeedsAvailable);
            Assert.Equal(600m, vm.WantsAvailable);
            Assert.Equal(400m, vm.InvestAvailable);
        });
    }

    [Fact]
    public void LoadAsync_KeepsAvailableValuesStableWhenBalanceReflectsLoggedExpenses()
    {
        RunInSta(() =>
        {
            var messenger = new WeakReferenceMessenger();
            var source = new SpendingSourceVM
            {
                Id = 1,
                Name = "Checking",
                SpendingSourceType = SpendingSourceType.Checking,
                Balance = 1825m,
                IsEnabled = true,
                ShowOnUI = true
            };
            var vm = CreateVm(messenger, CreateExpenseLogs(), CreateTags(), [source]);

            vm.LoadAsync().GetAwaiter().GetResult();

            Assert.Equal(2000m, vm.TotalIncomeAmount);
            Assert.Equal(1000m, vm.NeedsAvailable);
            Assert.Equal(600m, vm.WantsAvailable);
            Assert.Equal(400m, vm.InvestAvailable);
        });
    }

    [Fact]
    public void RecordLogMemoryMessage_AddExpenseKeepsAvailableValuesStableWithoutReload()
    {
        RunInSta(() =>
        {
            var messenger = new WeakReferenceMessenger();
            var source = new SpendingSourceVM
            {
                Id = 1,
                Name = "Checking",
                SpendingSourceType = SpendingSourceType.Checking,
                Balance = 2000m,
                IsEnabled = true,
                ShowOnUI = true
            };
            var vm = CreateVm(messenger, [], CreateTags(), [source]);
            vm.LoadAsync().GetAwaiter().GetResult();

            messenger.Send(new RecordLogMemoryMessage(new AddExpenseLogMemoryAction(
                new ExpenseLogMemorySnapshot(
                    ExpenseId: 99,
                    ExpenseLogId: 999,
                    ExpenseName: "Groceries",
                    Amount: 45m,
                    ExpenseCategory: ExpenseCategory.Needs,
                    SpendingSourceId: 1,
                    TagId: 1,
                    DeductedOn: DateTime.Today,
                    Notes: string.Empty,
                    IsForDeletion: false))));

            Assert.Equal(2000m, vm.TotalIncomeAmount);
            Assert.Equal(1000m, vm.NeedsAvailable);
            Assert.Equal(45m, vm.NeedsSpent);
            Assert.Equal(955m, vm.NeedsRemaining);
        });
    }

    [Fact]
    public void LoadAsync_UpdatesAvailableValuesWhenIncomeIncreasesBalance()
    {
        RunInSta(() =>
        {
            var messenger = new WeakReferenceMessenger();
            var source = new SpendingSourceVM
            {
                Id = 1,
                Name = "Checking",
                SpendingSourceType = SpendingSourceType.Checking,
                Balance = 2100m,
                IsEnabled = true,
                ShowOnUI = true
            };
            var vm = CreateVm(messenger, [], CreateTags(), [source]);

            vm.LoadAsync().GetAwaiter().GetResult();

            Assert.Equal(2100m, vm.TotalIncomeAmount);
            Assert.Equal(1050m, vm.NeedsAvailable);
            Assert.Equal(630m, vm.WantsAvailable);
            Assert.Equal(420m, vm.InvestAvailable);
        });
    }

    [Fact]
    public void LoadAsync_TotalIncomeAmountUsesAllocationLimitWhenConfigured()
    {
        RunInSta(() =>
        {
            var messenger = new WeakReferenceMessenger();
            var source = new SpendingSourceVM
            {
                Id = 1,
                Name = "Checking",
                SpendingSourceType = SpendingSourceType.Checking,
                Balance = 17_660_000m,
                IsEnabled = true,
                ShowOnUI = true
            };
            var vm = CreateVm(
                messenger,
                [],
                CreateTags(),
                [source],
                budgetAllocation: new BudgetAllocation
                {
                    AllocationLimit = 10_000_000m,
                    NeedsThreshold = 50,
                    WantsThreshold = 30,
                    InvestThreshold = 20
                });

            vm.LoadAsync().GetAwaiter().GetResult();

            Assert.Equal(10_000_000m, vm.TotalIncomeAmount);
            Assert.Equal(5_000_000m, vm.NeedsAvailable);
            Assert.Equal(3_000_000m, vm.WantsAvailable);
            Assert.Equal(2_000_000m, vm.InvestAvailable);
        });
    }

    [Fact]
    public void RecordLogMemoryMessage_AddIncomeUpdatesAvailableValuesWithoutReload()
    {
        RunInSta(() =>
        {
            var messenger = new WeakReferenceMessenger();
            var source = new SpendingSourceVM
            {
                Id = 1,
                Name = "Checking",
                SpendingSourceType = SpendingSourceType.Checking,
                Balance = 2000m,
                IsEnabled = true,
                ShowOnUI = true
            };
            var vm = CreateVm(messenger, [], CreateTags(), [source]);
            vm.LoadAsync().GetAwaiter().GetResult();

            messenger.Send(new RecordLogMemoryMessage(new AddIncomeLogMemoryAction(
                new IncomeLogMemorySnapshot(
                    IncomeLogId: 10,
                    SpendingSourceId: 1,
                    Name: "Paycheck",
                    Amount: 100m,
                    AddedOn: new DateTime(2026, 4, 10),
                    Notes: string.Empty))));

            Assert.Equal(2100m, vm.TotalIncomeAmount);
            Assert.Equal(1050m, vm.NeedsAvailable);
            Assert.Equal(630m, vm.WantsAvailable);
            Assert.Equal(420m, vm.InvestAvailable);
        });
    }

    [Fact]
    public void DeleteExpenseLogCommand_RestoresSourceTotalsAndBudgetMetricsInUi()
    {
        RunInSta(() =>
        {
            var messenger = new WeakReferenceMessenger();
            var vm = CreateVm(messenger, CreateExpenseLogs(), CreateTags(), CreateSpendingSources());
            vm.LoadAsync().GetAwaiter().GetResult();
            messenger.Send(new DateRangeSelectionChangedMessage(
                new DateTime(2026, 4, 10),
                new DateTime(2026, 4, 18)));
            var targetLog = vm.GetAllExpenseLogs().Single(log => log.Id == 1);
            var source = vm.SpendingSources.Single();

            Assert.Equal(-175m, source.Difference);

            vm.DeleteExpenseLogCommand.ExecuteAsync(targetLog).GetAwaiter().GetResult();

            Assert.Equal(2000m, vm.TotalIncomeAmount);
            Assert.Equal(1000m, vm.NeedsAvailable);
            Assert.Equal(0m, vm.NeedsSpent);
            Assert.Equal(1000m, vm.NeedsRemaining);
            Assert.Equal(-130m, source.Difference);
            Assert.DoesNotContain(vm.GetAllExpenseLogs(), log => log.Id == targetLog.Id);
        });
    }

    [Fact]
    public void RecordLogMemoryMessage_DecreasesDifferenceForVisibleIncomeWithoutReload()
    {
        RunInSta(() =>
        {
            var messenger = new WeakReferenceMessenger();
            var vm = CreateVm(messenger, CreateExpenseLogs(), CreateTags(), CreateSpendingSources(), CreateIncomeLogs());
            vm.LoadAsync().GetAwaiter().GetResult();

            messenger.Send(new DateRangeSelectionChangedMessage(
                new DateTime(2026, 4, 10),
                new DateTime(2026, 4, 12)));

            var source = vm.SpendingSources.Single();
            Assert.Equal(-55m, source.Difference);

            messenger.Send(new RecordLogMemoryMessage(new AddIncomeLogMemoryAction(
                new IncomeLogMemorySnapshot(
                    999,
                    source.Id,
                    "Bonus",
                    15m,
                    new DateTime(2026, 4, 12),
                    "bonus"))));

            Assert.Equal(-70m, source.Difference);
        });
    }

    [Fact]
    public void RecordLogMemoryMessage_EditIncomeUpdatesTrackedIncomeWithoutReload()
    {
        RunInSta(() =>
        {
            var messenger = new WeakReferenceMessenger();
            var vm = CreateVm(messenger, CreateExpenseLogs(), CreateTags(), CreateSpendingSources(), CreateIncomeLogs());
            vm.LoadAsync().GetAwaiter().GetResult();

            var before = new IncomeLogMemorySnapshot(
                10,
                1,
                "Refund",
                20m,
                new DateTime(2026, 4, 12),
                "refund");
            var after = before with
            {
                Name = "Bonus",
                Amount = 50m
            };

            messenger.Send(new RecordLogMemoryMessage(new EditIncomeLogMemoryAction(before, after)));

            var updated = vm.GetAllIncomeLogs().Single(log => log.Id == 10);
            Assert.Equal("Bonus", updated.Name);
            Assert.Equal(50m, updated.Amount);
            Assert.Equal(2030m, vm.TotalIncomeAmount);
        });
    }

    [Fact]
    public void RecordLogMemoryMessage_DeleteIncomeRemovesTrackedIncomeWithoutReload()
    {
        RunInSta(() =>
        {
            var messenger = new WeakReferenceMessenger();
            var vm = CreateVm(messenger, CreateExpenseLogs(), CreateTags(), CreateSpendingSources(), CreateIncomeLogs());
            vm.LoadAsync().GetAwaiter().GetResult();

            messenger.Send(new RecordLogMemoryMessage(new DeleteIncomeLogMemoryAction(
                new IncomeLogMemorySnapshot(
                    10,
                    1,
                    "Refund",
                    20m,
                    new DateTime(2026, 4, 12),
                    "refund"))));

            Assert.Empty(vm.GetAllIncomeLogs());
            Assert.Equal(1980m, vm.TotalIncomeAmount);
        });
    }

    [Fact]
    public void RecordLogMemoryMessage_EditExpensePreservesTagColorAndSourceNameWithoutReload()
    {
        RunInSta(() =>
        {
            var messenger = new WeakReferenceMessenger();
            var vm = CreateVm(messenger, CreateExpenseLogs(), CreateTags(), CreateSpendingSources());
            vm.LoadAsync().GetAwaiter().GetResult();

            var before = new ExpenseLogMemorySnapshot(
                ExpenseId: 11,
                ExpenseLogId: 1,
                ExpenseName: "Groceries",
                Amount: 45m,
                ExpenseCategory: ExpenseCategory.Needs,
                SpendingSourceId: 1,
                TagId: 1,
                DeductedOn: new DateTime(2026, 4, 10),
                Notes: string.Empty,
                IsForDeletion: false);

            var after = before with
            {
                TagId = 2,
                ExpenseName = "Movie",
                Amount = 30m,
                DeductedOn = new DateTime(2026, 4, 12)
            };

            messenger.Send(new RecordLogMemoryMessage(new EditExpenseLogMemoryAction(before, after)));

            var updated = vm.GetAllExpenseLogs().Single(log => log.Id == 1);
            Assert.Equal("Checking", updated.SpendingSource.Name);
            Assert.Equal("#F97316", updated.Expense.ExpenseTag.HexCode);
        });
    }

    private static BudgetAllocationPanelVM CreateVm(
        IMessenger messenger,
        IReadOnlyList<ExpenseLogVM> expenseLogs,
        IReadOnlyList<ExpenseTagVM> tags,
        IReadOnlyList<SpendingSourceVM> spendingSources,
        IReadOnlyList<IncomeLogVM>? incomeLogs = null,
        IReadOnlyList<UserSettings>? settings = null,
        BudgetAllocation? budgetAllocation = null)
    {
        var expenseLogService = Substitute.For<IExpenseLogService>();
        expenseLogService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ExpenseLogDto>>([]));
        expenseLogService.DeleteAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var spendingSourceService = Substitute.For<ISpendingSourceService>();
        spendingSourceService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<SpendingSourceDto>>([]));

        var tagService = Substitute.For<ITagService>();
        tagService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ExpenseTagDto>>([]));

        var userSettingsRepository = Substitute.For<Fluxo.Core.Interfaces.Repositories.IUserSettingsRepository>();
        userSettingsRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<UserSettings>>(settings ?? []));
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.UserSettings.Returns(userSettingsRepository);
        var budgetAllocationRepository = Substitute.For<Fluxo.Core.Interfaces.Repositories.IBudgetAllocationRepository>();
        budgetAllocationRepository.GetAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<BudgetAllocation?>(budgetAllocation ?? new BudgetAllocation()));
        unitOfWork.BudgetAllocation.Returns(budgetAllocationRepository);
        var incomeLogRepository = Substitute.For<Fluxo.Core.Interfaces.Repositories.IIncomeLogRepository>();
        incomeLogRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<IncomeLog>>(
                (incomeLogs ?? []).Select(log => new IncomeLog
                {
                    Id = log.Id,
                    Name = log.Name,
                    Amount = log.Amount,
                    AddedOn = log.AddedOn,
                    Notes = log.Notes,
                    SpendingSourceId = log.SpendingSource.Id,
                    SpendingSource = new SpendingSource
                    {
                        Id = log.SpendingSource.Id,
                        Name = log.SpendingSource.Name,
                        SpendingSourceType = log.SpendingSource.SpendingSourceType
                    }
                }).ToList()));
        unitOfWork.IncomeLogs.Returns(incomeLogRepository);
        var dataOperationRunner = new InlineDataOperationRunner(unitOfWork);

        var mapper = Substitute.For<IMapper>();
        mapper.Map<IReadOnlyList<ExpenseLogVM>>(Arg.Any<object>()).Returns(expenseLogs);
        mapper.Map<IReadOnlyList<SpendingSourceVM>>(Arg.Any<object>()).Returns(spendingSources);
        mapper.Map<IReadOnlyList<ExpenseTagVM>>(Arg.Any<object>()).Returns(tags);

        return new BudgetAllocationPanelVM(
            expenseLogService,
            spendingSourceService,
            tagService,
            dataOperationRunner,
            mapper,
            messenger);
    }

    private static IReadOnlyList<ExpenseLogVM> CreateExpenseLogs()
    {
        var groceries = new ExpenseTagVM { Id = 1, Name = "Groceries", HexCode = "#22C55E" };
        var fun = new ExpenseTagVM { Id = 2, Name = "Fun", HexCode = "#F97316" };
        var source = new SpendingSourceVM
        {
            Id = 1,
            Name = "Checking",
            SpendingSourceType = SpendingSourceType.Checking,
            Balance = 2000m,
            IsEnabled = true,
            ShowOnUI = true
        };

        return
        [
            new ExpenseLogVM
            {
                Id = 1,
                Amount = 45m,
                DeductedOn = new DateTime(2026, 4, 10),
                Expense = new ExpenseVM
                {
                    Id = 11,
                    Name = "Groceries",
                    ExpenseCategory = ExpenseCategory.Needs,
                    ExpenseTag = groceries
                },
                SpendingSource = source
            },
            new ExpenseLogVM
            {
                Id = 2,
                Amount = 30m,
                DeductedOn = new DateTime(2026, 4, 12),
                Expense = new ExpenseVM
                {
                    Id = 12,
                    Name = "Movie",
                    ExpenseCategory = ExpenseCategory.Wants,
                    ExpenseTag = fun
                },
                SpendingSource = source
            },
            new ExpenseLogVM
            {
                Id = 3,
                Amount = 100m,
                DeductedOn = new DateTime(2026, 4, 18),
                Expense = new ExpenseVM
                {
                    Id = 13,
                    Name = "ETF",
                    ExpenseCategory = ExpenseCategory.Savings,
                    ExpenseTag = groceries
                },
                SpendingSource = source
            }
        ];
    }

    private static IReadOnlyList<ExpenseTagVM> CreateTags()
    {
        return
        [
            new ExpenseTagVM { Id = 1, Name = "Groceries", HexCode = "#22C55E" },
            new ExpenseTagVM { Id = 2, Name = "Fun", HexCode = "#F97316" }
        ];
    }

    private static IReadOnlyList<SpendingSourceVM> CreateSpendingSources()
    {
        return
        [
            new SpendingSourceVM
            {
                Id = 1,
                Name = "Checking",
                SpendingSourceType = SpendingSourceType.Checking,
                Balance = 1825m,
                IsEnabled = true,
                ShowOnUI = true
            }
        ];
    }

    private static IReadOnlyList<IncomeLogVM> CreateIncomeLogs()
    {
        return
        [
            new IncomeLogVM
            {
                Id = 10,
                Name = "Refund",
                Amount = 20m,
                AddedOn = new DateTime(2026, 4, 12),
                Notes = "refund",
                SpendingSource = new SpendingSourceVM
                {
                    Id = 1,
                    Name = "Checking",
                    SpendingSourceType = SpendingSourceType.Checking
                }
            }
        ];
    }

    private static IReadOnlyList<ExpenseTagVM> CreateTagsForUsageOrdering()
    {
        return
        [
            new ExpenseTagVM { Id = 1, Name = "Groceries", HexCode = "#22C55E" },
            new ExpenseTagVM { Id = 2, Name = "Transport", HexCode = "#06B6D4" },
            new ExpenseTagVM { Id = 3, Name = "Dining", HexCode = "#F97316" },
            new ExpenseTagVM { Id = 4, Name = "Bills", HexCode = "#0EA5E9" },
            new ExpenseTagVM { Id = 5, Name = "Health", HexCode = "#10B981" },
            new ExpenseTagVM { Id = 6, Name = "System", HexCode = "#9333EA", IsSystemTag = true }
        ];
    }

    private static IReadOnlyList<ExpenseLogVM> CreateExpenseLogsForUsageOrdering()
    {
        var tags = CreateTagsForUsageOrdering().ToDictionary(tag => tag.Id);
        var source = CreateSpendingSources().Single();
        var logs = new List<ExpenseLogVM>();
        var nextId = 1;

        AddLogs(logs, ref nextId, tags[1], 4, source);
        AddLogs(logs, ref nextId, tags[2], 3, source);
        AddLogs(logs, ref nextId, tags[3], 2, source);
        AddLogs(logs, ref nextId, tags[4], 1, source);
        AddLogs(logs, ref nextId, tags[5], 1, source);
        AddLogs(logs, ref nextId, tags[6], 8, source);

        return logs;
    }

    private static IReadOnlyList<ExpenseTagVM> CreateTagsForTagPromotion()
    {
        return
        [
            new ExpenseTagVM { Id = 1, Name = "Groceries", HexCode = "#22C55E" },
            new ExpenseTagVM { Id = 2, Name = "Transport", HexCode = "#06B6D4" },
            new ExpenseTagVM { Id = 3, Name = "Dining", HexCode = "#F97316" },
            new ExpenseTagVM { Id = 4, Name = "Bills", HexCode = "#0EA5E9" },
            new ExpenseTagVM { Id = 5, Name = "Health", HexCode = "#10B981" },
            new ExpenseTagVM { Id = 6, Name = "Pets", HexCode = "#A855F7" }
        ];
    }

    private static IReadOnlyList<ExpenseLogVM> CreateExpenseLogsForTagPromotion()
    {
        var tags = CreateTagsForTagPromotion().ToDictionary(tag => tag.Id);
        var source = CreateSpendingSources().Single();
        var logs = new List<ExpenseLogVM>();
        var nextId = 1;

        AddLogs(logs, ref nextId, tags[1], 6, source);
        AddLogs(logs, ref nextId, tags[2], 5, source);
        AddLogs(logs, ref nextId, tags[3], 4, source);
        AddLogs(logs, ref nextId, tags[4], 3, source);
        AddLogs(logs, ref nextId, tags[5], 2, source);
        AddLogs(logs, ref nextId, tags[6], 1, source);

        return logs;
    }

    private static void AddLogs(
        ICollection<ExpenseLogVM> logs,
        ref int nextId,
        ExpenseTagVM tag,
        int count,
        SpendingSourceVM source)
    {
        for (var index = 0; index < count; index++)
        {
            logs.Add(new ExpenseLogVM
            {
                Id = nextId++,
                Amount = 10m + index,
                DeductedOn = new DateTime(2026, 4, 1).AddDays(index),
                Expense = new ExpenseVM
                {
                    Id = 1000 + nextId,
                    Name = $"{tag.Name} #{index + 1}",
                    ExpenseCategory = ExpenseCategory.Needs,
                    ExpenseTag = tag
                },
                SpendingSource = source
            });
        }
    }

    private static List<ExpenseLogVM> GetItems(ICollectionView view)
    {
        return view.Cast<ExpenseLogVM>().ToList();
    }

    private static void RunInSta(Action action)
    {
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception exception) { failure = exception; }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
