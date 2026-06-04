using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Windows.Data;
using AutoMapper;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.DTO;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Mappings;
using Fluxo.Resources.Resources.Messages;
using Fluxo.Services.Mappings;
using Fluxo.Tests.TestDoubles;
using Fluxo.ViewModels.Shell.Main;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Fluxo.Tests.ViewModels.Shell.Main;

public sealed class LedgerVMTests
{
    [Fact]
    public void SearchText_FiltersByAnywhereNameMatchOnly()
    {
        RunInSta(() =>
        {
            var vm = CreateVm();
            vm.LoadAsync().GetAwaiter().GetResult();

            vm.SearchText = "flix";

            var item = Assert.Single(GetItems(vm.TransactionsView));
            Assert.Equal("Netflix", item.Name);
        });
    }

    [Fact]
    public void CheckingEverySpecificCategory_NormalizesBackToAll()
    {
        RunInSta(() =>
        {
            var vm = CreateVm();
            vm.LoadAsync().GetAwaiter().GetResult();

            foreach (var option in vm.CategoryFilters.Where(option => !option.IsAll))
                option.IsChecked = true;

            Assert.True(vm.CategoryFilters.Single(option => option.IsAll).IsChecked);
            Assert.All(vm.CategoryFilters.Where(option => !option.IsAll), option => Assert.False(option.IsChecked));
        });
    }

    [Fact]
    public void AnySpecificSelection_UnchecksAll()
    {
        RunInSta(() =>
        {
            var vm = CreateVm();
            vm.LoadAsync().GetAwaiter().GetResult();

            vm.TypeFilters.Single(option => option.Value == LedgerTransactionKind.Income).IsChecked = true;

            Assert.False(vm.TypeFilters.Single(option => option.IsAll).IsChecked);
            var item = Assert.Single(GetItems(vm.TransactionsView));
            Assert.Equal(LedgerTransactionKind.Income, item.Kind);
        });
    }

    [Fact]
    public void DateRangeMessage_ReloadsPeriodDataButSearchDoesNot()
    {
        RunInSta(() =>
        {
            var expenseLogService = Substitute.For<IExpenseLogService>();
            expenseLogService.GetAllAsync(Arg.Any<CancellationToken>())
                .Returns(CreateExpenseLogs());
            var vm = CreateVm(expenseLogService: expenseLogService);
            vm.LoadAsync().GetAwaiter().GetResult();

            vm.SearchText = "fresh";
            vm.SearchText = string.Empty;
            expenseLogService.Received(1).GetAllAsync(Arg.Any<CancellationToken>());

            vm.Receive(new DateRangeSelectionChangedMessage(new DateTime(2026, 6, 2), new DateTime(2026, 6, 2)));
            SpinWait.SpinUntil(() =>
            {
                try
                {
                    expenseLogService.Received(2).GetAllAsync(Arg.Any<CancellationToken>());
                    return true;
                }
                catch
                {
                    return false;
                }
            }, TimeSpan.FromSeconds(2));

            expenseLogService.Received(2).GetAllAsync(Arg.Any<CancellationToken>());
            Assert.All(GetItems(vm.TransactionsView), item => Assert.Equal(new DateTime(2026, 6, 2), item.OccurredOn.Date));
        });
    }

    [Fact]
    public void GroupingByDate_UsesMonthDayHeaderAndSortsAmountsInsideGroup()
    {
        RunInSta(() =>
        {
            var vm = CreateVm();
            vm.LoadAsync().GetAwaiter().GetResult();
            vm.SelectedGroupingMode = LedgerGroupingMode.Date;
            vm.AmountSortDirection = LedgerAmountSortDirection.Ascending;

            var groups = vm.TransactionsView.Groups!.Cast<CollectionViewGroup>().ToList();
            var jun3 = groups.Single(group => (string)group.Name == "JUN 03");
            var items = jun3.Items.Cast<LedgerTransactionItemVM>().ToList();

            Assert.Equal(new[] { "FreshMart Grocery", "Netflix", "June Salary" }, items.Select(item => item.Name));
            Assert.Equal(new[] { -67.50m, -15.99m, 2800m }, items.Select(item => item.SignedAmount));
        });
    }

    [Fact]
    public void LoadAsync_ShowsGoalLogsAsExpensesWithGoalMarkerAndSummaryAmount()
    {
        RunInSta(() =>
        {
            var vm = CreateVm();
            vm.LoadAsync().GetAwaiter().GetResult();

            var goal = GetItems(vm.TransactionsView).Single(item => item.IsGoal);

            Assert.Equal(LedgerTransactionKind.Expense, goal.Kind);
            Assert.Equal(100m, vm.GoalAmount);
            Assert.Equal(83.49m, vm.SpentAmount);
            Assert.Equal(2800m, vm.EarnedAmount);
            Assert.Equal(2616.51m, vm.NetAmount);
        });
    }

    private static LedgerVM CreateVm(
        IExpenseLogService? expenseLogService = null,
        ISpendingSourceService? spendingSourceService = null,
        ITagService? tagService = null,
        IUnitOfWork? unitOfWork = null)
    {
        expenseLogService ??= Substitute.For<IExpenseLogService>();
        expenseLogService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(CreateExpenseLogs());

        spendingSourceService ??= Substitute.For<ISpendingSourceService>();
        spendingSourceService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(CreateSpendingSources());

        tagService ??= Substitute.For<ITagService>();
        tagService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(CreateTags());

        unitOfWork ??= CreateUnitOfWork();

        return new LedgerVM(
            expenseLogService,
            spendingSourceService,
            tagService,
            new InlineDataOperationRunner(unitOfWork),
            CreateMapper(),
            new WeakReferenceMessenger());
    }

    private static IMapper CreateMapper()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<EntityDtoProfile>();
            cfg.AddProfile<DtoViewModelProfile>();
        }, NullLoggerFactory.Instance);
        return config.CreateMapper();
    }

    private static IUnitOfWork CreateUnitOfWork()
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var incomeLogs = Substitute.For<IIncomeLogRepository>();
        incomeLogs.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(CreateIncomeLogs());
        unitOfWork.IncomeLogs.Returns(incomeLogs);
        return unitOfWork;
    }

    private static IReadOnlyList<ExpenseLogDto> CreateExpenseLogs()
    {
        var groceries = new ExpenseTagDto { Id = 1, Name = "Groceries", HexCode = "#53A96B" };
        var streaming = new ExpenseTagDto { Id = 2, Name = "Streaming", HexCode = "#D97936" };
        var goal = new ExpenseTagDto { Id = 3, Name = "Goal Update", HexCode = "#EAABF2", IsSystemTag = true };
        var checking = new SpendingSourceDto { Id = 1, Name = "DBS Checking", SpendingSourceType = SpendingSourceType.Checking, IsEnabled = true };
        var credit = new SpendingSourceDto { Id = 2, Name = "Citibank Credit", SpendingSourceType = SpendingSourceType.Credit, IsEnabled = false };

        return
        [
            CreateExpenseLog(1, "FreshMart Grocery", 67.50m, new DateTime(2026, 6, 3, 9, 15, 0), ExpenseCategory.Needs, groceries, checking),
            CreateExpenseLog(2, "Netflix", 15.99m, new DateTime(2026, 6, 3, 11, 30, 0), ExpenseCategory.Wants, streaming, credit, "Recurring transaction"),
            CreateExpenseLog(3, "Vacation Goal", 100m, new DateTime(2026, 6, 2, 8, 0, 0), ExpenseCategory.Savings, goal, checking)
        ];
    }

    private static ExpenseLogDto CreateExpenseLog(
        int id,
        string name,
        decimal amount,
        DateTime deductedOn,
        ExpenseCategory category,
        ExpenseTagDto tag,
        SpendingSourceDto source,
        string notes = "")
    {
        return new ExpenseLogDto
        {
            Id = id,
            Amount = amount,
            DeductedOn = deductedOn,
            Notes = notes,
            Expense = new ExpenseDto
            {
                Id = id,
                Name = name,
                Amount = amount,
                ExpenseCategory = category,
                ExpenseTag = tag,
                ExpenseTagId = tag.Id,
                SpendingSource = source,
                SpendingSourceId = source.Id
            },
            SpendingSource = source,
            SpendingSourceId = source.Id
        };
    }

    private static IReadOnlyList<IncomeLog> CreateIncomeLogs()
    {
        return
        [
            new IncomeLog
            {
                Id = 10,
                Name = "June Salary",
                Amount = 2800m,
                AddedOn = new DateTime(2026, 6, 3, 14, 0, 0),
                SpendingSourceId = 1,
                SpendingSource = new SpendingSource
                {
                    Id = 1,
                    Name = "DBS Checking",
                    SpendingSourceType = SpendingSourceType.Checking,
                    IsEnabled = true
                }
            }
        ];
    }

    private static IReadOnlyList<SpendingSourceDto> CreateSpendingSources()
    {
        return
        [
            new SpendingSourceDto { Id = 1, Name = "DBS Checking", SpendingSourceType = SpendingSourceType.Checking, IsEnabled = true },
            new SpendingSourceDto { Id = 2, Name = "Citibank Credit", SpendingSourceType = SpendingSourceType.Credit, IsEnabled = false }
        ];
    }

    private static IReadOnlyList<ExpenseTagDto> CreateTags()
    {
        return
        [
            new ExpenseTagDto { Id = 1, Name = "Groceries", HexCode = "#53A96B" },
            new ExpenseTagDto { Id = 2, Name = "Streaming", HexCode = "#D97936" },
            new ExpenseTagDto { Id = 3, Name = "Goal Update", HexCode = "#EAABF2", IsSystemTag = true }
        ];
    }

    private static IReadOnlyList<LedgerTransactionItemVM> GetItems(ICollectionView view)
    {
        return view.Cast<LedgerTransactionItemVM>().ToList();
    }

    private static void RunInSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            ExceptionDispatchInfo.Capture(exception).Throw();
    }
}
