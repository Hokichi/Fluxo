using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Windows.Data;
using AutoMapper;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Constants;
using Fluxo.Core.DTO;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Resources.Messages;
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
    public void LoadAsync_UsesBudgetThresholdsFromUserSettings()
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
                    new UserSettings { Name = UserSettingNames.NeedsThreshold, Value = "40" },
                    new UserSettings { Name = UserSettingNames.WantsThreshold, Value = "35" },
                    new UserSettings { Name = UserSettingNames.InvestThreshold, Value = "25" }
                ]);

            vm.LoadAsync().GetAwaiter().GetResult();

            Assert.Equal(800m, vm.NeedsAvailable);
            Assert.Equal(700m, vm.WantsAvailable);
            Assert.Equal(500m, vm.InvestAvailable);
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
                    new UserSettings { Name = UserSettingNames.NeedsThreshold, Value = "invalid" }
                ]);

            vm.LoadAsync().GetAwaiter().GetResult();

            Assert.Equal(1000m, vm.NeedsAvailable);
            Assert.Equal(600m, vm.WantsAvailable);
            Assert.Equal(400m, vm.InvestAvailable);
        });
    }

    [Fact]
    public void DeleteExpenseLogCommand_RestoresSpendingSourceTotalsInUi()
    {
        RunInSta(() =>
        {
            var messenger = new WeakReferenceMessenger();
            var vm = CreateVm(messenger, CreateExpenseLogs(), CreateTags(), CreateSpendingSources());
            vm.LoadAsync().GetAwaiter().GetResult();
            var targetLog = vm.GetAllExpenseLogs().Single(log => log.Id == 1);

            vm.DeleteExpenseLogCommand.ExecuteAsync(targetLog).GetAwaiter().GetResult();

            Assert.Equal(2045m, vm.TotalIncomeAmount);
            Assert.DoesNotContain(vm.GetAllExpenseLogs(), log => log.Id == targetLog.Id);
        });
    }

    [Fact]
    public void RecordLogMemoryMessage_AdjustsDifferenceForAffectedSourceWithoutReload()
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
            Assert.Equal(55m, source.Difference);

            messenger.Send(new RecordLogMemoryMessage(new AddIncomeLogMemoryAction(
                new IncomeLogMemorySnapshot(
                    999,
                    source.Id,
                    15m,
                    new DateTime(2026, 4, 12),
                    "bonus"))));

            Assert.Equal(40m, source.Difference);
        });
    }

    private static BudgetAllocationPanelVM CreateVm(
        IMessenger messenger,
        IReadOnlyList<ExpenseLogVM> expenseLogs,
        IReadOnlyList<ExpenseTagVM> tags,
        IReadOnlyList<SpendingSourceVM> spendingSources,
        IReadOnlyList<IncomeLogVM>? incomeLogs = null,
        IReadOnlyList<UserSettings>? settings = null)
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
        var incomeLogRepository = Substitute.For<Fluxo.Core.Interfaces.Repositories.IIncomeLogRepository>();
        incomeLogRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<IncomeLog>>(
                (incomeLogs ?? []).Select(log => new IncomeLog
                {
                    Id = log.Id,
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
                Balance = 2000m,
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
