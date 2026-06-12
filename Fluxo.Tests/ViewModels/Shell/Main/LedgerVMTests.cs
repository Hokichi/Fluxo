using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Text;
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
using Fluxo.Services.History;
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
    public void AnySpecificSelection_UnchecksAllButDoesNotFilterUntilApplied()
    {
        RunInSta(() =>
        {
            var vm = CreateVm();
            vm.LoadAsync().GetAwaiter().GetResult();

            vm.TypeFilters.Single(option => option.Value == LedgerTransactionKind.Income).IsChecked = true;

            Assert.False(vm.TypeFilters.Single(option => option.IsAll).IsChecked);
            Assert.Contains(GetItems(vm.TransactionsView), item => item.Kind == LedgerTransactionKind.Expense);

            vm.ApplyFilters();

            var item = Assert.Single(GetItems(vm.TransactionsView));
            Assert.Equal(LedgerTransactionKind.Income, item.Kind);
        });
    }

    [Fact]
    public void FilterSelectionSnapshot_TracksOnlyNetDropdownSelectionChanges()
    {
        RunInSta(() =>
        {
            var vm = CreateVm();
            vm.LoadAsync().GetAwaiter().GetResult();

            Assert.False(vm.HasPendingFilterChanges);

            var incomeFilter = vm.TypeFilters.Single(option => option.Value == LedgerTransactionKind.Income);
            incomeFilter.IsChecked = true;

            Assert.True(vm.HasPendingFilterChanges);
            Assert.True(vm.ApplyFiltersIfChanged());
            Assert.False(vm.HasPendingFilterChanges);
            Assert.False(vm.ApplyFiltersIfChanged());

            incomeFilter.IsChecked = false;

            Assert.True(vm.HasPendingFilterChanges);
            incomeFilter.IsChecked = true;
            Assert.False(vm.HasPendingFilterChanges);
        });
    }

    [Fact]
    public void MultipleSpecificTagSelections_FilterAsAnySelectedTagWhenApplied()
    {
        RunInSta(() =>
        {
            var vm = CreateVm();
            vm.LoadAsync().GetAwaiter().GetResult();

            vm.TypeFilters.Single(option => !option.IsAll && option.Value == LedgerTransactionKind.Expense).IsChecked = true;
            vm.TagFilters.Single(option => option.Value == 1).IsChecked = true;
            vm.TagFilters.Single(option => option.Value == 2).IsChecked = true;
            vm.ApplyFilters();

            Assert.Equal(
                new[] { "FreshMart Grocery", "Netflix" },
                GetItems(vm.TransactionsView).Select(item => item.Name).OrderBy(name => name).ToArray());
        });
    }

    [Fact]
    public void FilterSelectionBadgesAndTooltips_HideForAllAndListSpecificSelections()
    {
        RunInSta(() =>
        {
            var vm = CreateVm();
            vm.LoadAsync().GetAwaiter().GetResult();

            Assert.Equal(0, vm.TypeFilterSelectionCount);
            Assert.Null(vm.TypeFilterSelectionToolTip);

            vm.TypeFilters.Single(option => option.Value == LedgerTransactionKind.Income).IsChecked = true;
            vm.TagFilters.Single(option => option.Value == 1).IsChecked = true;
            vm.TagFilters.Single(option => option.Value == 2).IsChecked = true;

            Assert.Equal(1, vm.TypeFilterSelectionCount);
            Assert.Equal("Incomes", vm.TypeFilterSelectionToolTip);
            Assert.Equal(2, vm.TagFilterSelectionCount);
            Assert.Equal($"Groceries{Environment.NewLine}Streaming", vm.TagFilterSelectionToolTip);

            vm.ClearFilters();

            Assert.Equal(0, vm.TypeFilterSelectionCount);
            Assert.Null(vm.TypeFilterSelectionToolTip);
            Assert.Equal(0, vm.TagFilterSelectionCount);
            Assert.Null(vm.TagFilterSelectionToolTip);
        });
    }

    [Fact]
    public void ClearFilters_ResetsDropdownFiltersAndSearchButPreservesGrouping()
    {
        RunInSta(() =>
        {
            var vm = CreateVm();
            vm.LoadAsync().GetAwaiter().GetResult();
            vm.SearchText = "flix";
            vm.SelectedGroupingMode = LedgerGroupingMode.SpendingSources;
            vm.TypeFilters.Single(option => option.Value == LedgerTransactionKind.Income).IsChecked = true;
            vm.ApplyFilters();

            vm.ClearFilters();

            Assert.Equal(string.Empty, vm.SearchText);
            Assert.Equal(LedgerGroupingMode.SpendingSources, vm.SelectedGroupingMode);
            Assert.True(vm.TypeFilters.Single(option => option.IsAll).IsChecked);
            Assert.True(vm.SpendingSourceFilters.Single(option => option.IsAll).IsChecked);
            Assert.True(vm.CategoryFilters.Single(option => option.IsAll).IsChecked);
            Assert.True(vm.TagFilters.Single(option => option.IsAll).IsChecked);
            Assert.Equal(4, GetItems(vm.TransactionsView).Count);
        });
    }

    [Fact]
    public void ApplyTagFilter_SelectsClickedTagOnTopOfCurrentFilterAndRefreshesImmediately()
    {
        RunInSta(() =>
        {
            var vm = CreateVm();
            vm.LoadAsync().GetAwaiter().GetResult();
            vm.TypeFilters.Single(option => !option.IsAll && option.Value == LedgerTransactionKind.Expense).IsChecked = true;
            vm.ApplyFilters();

            vm.ApplyTagFilter(2);

            Assert.True(vm.TypeFilters.Single(option => !option.IsAll && option.Value == LedgerTransactionKind.Expense).IsChecked);
            Assert.False(vm.TagFilters.Single(option => option.IsAll).IsChecked);
            Assert.True(vm.TagFilters.Single(option => option.Value == 2).IsChecked);
            var item = Assert.Single(GetItems(vm.TransactionsView));
            Assert.Equal("Netflix", item.Name);
        });
    }

    [Fact]
    public void ApplySpendingSourceFilter_SelectsClickedSourceOnTopOfCurrentFilterAndRefreshesImmediately()
    {
        RunInSta(() =>
        {
            var vm = CreateVm();
            vm.LoadAsync().GetAwaiter().GetResult();
            vm.CategoryFilters.Single(option => option.Value == ExpenseCategory.Wants).IsChecked = true;
            vm.ApplyFilters();

            vm.ApplySpendingSourceFilter(2);

            Assert.True(vm.CategoryFilters.Single(option => option.Value == ExpenseCategory.Wants).IsChecked);
            Assert.False(vm.SpendingSourceFilters.Single(option => option.IsAll).IsChecked);
            Assert.True(vm.SpendingSourceFilters.Single(option => option.Value == 2).IsChecked);
            var item = Assert.Single(GetItems(vm.TransactionsView));
            Assert.Equal("Netflix", item.Name);
        });
    }

    [Fact]
    public void HasTransactions_TracksRawLoadedLedgerData()
    {
        RunInSta(() =>
        {
            var vm = CreateVm();
            vm.LoadAsync().GetAwaiter().GetResult();

            Assert.True(vm.HasTransactions);

            vm.Receive(new DateRangeSelectionChangedMessage(new DateTime(2026, 6, 5), new DateTime(2026, 6, 5)));
            SpinWait.SpinUntil(() => !vm.HasTransactions, TimeSpan.FromSeconds(2));

            Assert.False(vm.HasTransactions);
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
    public void DateRangeMessage_UpdatesLedgerDateSelectorProperties()
    {
        RunInSta(() =>
        {
            var vm = CreateVm();

            vm.Receive(new DateRangeSelectionChangedMessage(new DateTime(2026, 6, 1), new DateTime(2026, 6, 3)));

            Assert.Equal(new DateTime(2026, 6, 1), vm.StartDate.Date);
            Assert.Equal(new DateTime(2026, 6, 3), vm.EndDate.Date);
        });
    }

    [Fact]
    public void DateSelectorPropertyChange_ReloadsLedgerForSelectedPeriod()
    {
        RunInSta(() =>
        {
            var expenseLogService = Substitute.For<IExpenseLogService>();
            expenseLogService.GetAllAsync(Arg.Any<CancellationToken>())
                .Returns(CreateExpenseLogs());
            var vm = CreateVm(expenseLogService: expenseLogService);
            vm.LoadAsync().GetAwaiter().GetResult();

            vm.StartDate = new DateTime(2026, 6, 2);
            vm.EndDate = new DateTime(2026, 6, 2);

            SpinWait.SpinUntil(() =>
            {
                try
                {
                    expenseLogService.Received(3).GetAllAsync(Arg.Any<CancellationToken>());
                    return true;
                }
                catch
                {
                    return false;
                }
            }, TimeSpan.FromSeconds(2));

            Assert.All(GetItems(vm.TransactionsView), item => Assert.Equal(new DateTime(2026, 6, 2), item.OccurredOn.Date));
        });
    }

    [Fact]
    public void AmountSort_DefaultsDescendingAndToggleSwitchesToAscending()
    {
        RunInSta(() =>
        {
            var vm = CreateVm();
            vm.LoadAsync().GetAwaiter().GetResult();
            vm.SelectedGroupingMode = LedgerGroupingMode.None;

            Assert.Equal(LedgerAmountSortDirection.Descending, vm.AmountSortDirection);
            Assert.Equal(
                new[] { 2800m, -15.99m, -67.50m, -100m },
                GetItems(vm.TransactionsView).Select(item => item.SignedAmount).ToArray());

            vm.ToggleAmountSortDirectionCommand.Execute(null);

            Assert.Equal(LedgerAmountSortDirection.Ascending, vm.AmountSortDirection);
            Assert.Equal(
                new[] { -100m, -67.50m, -15.99m, 2800m },
                GetItems(vm.TransactionsView).Select(item => item.SignedAmount).ToArray());
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

    [Fact]
    public void EditTransactionCommand_TogglesThenCommitsExpenseEdit()
    {
        RunInSta(() =>
        {
            var messenger = new WeakReferenceMessenger();
            var recipient = new MessageCaptureRecipient();
            messenger.Register<MessageCaptureRecipient, RecordLogMemoryMessage>(recipient,
                static (target, message) => target.RecordLogMemoryMessages.Add(message));
            var unitOfWork = CreateUnitOfWork();
            var vm = CreateVm(unitOfWork: unitOfWork, messenger: messenger);
            vm.LoadAsync().GetAwaiter().GetResult();
            var grocery = GetItems(vm.TransactionsView).Single(item => item.Name == "FreshMart Grocery");

            vm.EditTransactionCommand.ExecuteAsync(grocery).GetAwaiter().GetResult();
            grocery.Name = "FreshMart Market";
            grocery.Amount = 70m;
            grocery.TagId = 2;
            vm.EditTransactionCommand.ExecuteAsync(grocery).GetAwaiter().GetResult();

            Assert.False(grocery.IsEditing);
            Assert.Equal("FreshMart Market", grocery.Name);
            Assert.Equal("Streaming", grocery.TagName);
            var action = Assert.IsType<EditExpenseLogMemoryAction>(Assert.Single(recipient.RecordLogMemoryMessages).Value);
            Assert.Equal("FreshMart Grocery", action.Before.ExpenseName);
            Assert.Equal("FreshMart Market", action.After.ExpenseName);
            Assert.Equal(70m, action.After.Amount);
            Assert.Equal(2, action.After.TagId);
        });
    }

    [Fact]
    public void EditTransactionCommand_DisablesOtherRowsUntilEditIsSaved()
    {
        RunInSta(() =>
        {
            var vm = CreateVm(unitOfWork: CreateUnitOfWork());
            vm.LoadAsync().GetAwaiter().GetResult();
            var grocery = GetItems(vm.TransactionsView).Single(item => item.Name == "FreshMart Grocery");
            var netflix = GetItems(vm.TransactionsView).Single(item => item.Name == "Netflix");

            vm.EditTransactionCommand.ExecuteAsync(grocery).GetAwaiter().GetResult();

            Assert.True(grocery.IsEditing);
            Assert.False(grocery.IsDisabledByAnotherEdit);
            Assert.True(netflix.IsDisabledByAnotherEdit);
            Assert.Equal(grocery, vm.EditingTransaction);

            vm.EditTransactionCommand.ExecuteAsync(grocery).GetAwaiter().GetResult();

            Assert.False(grocery.IsEditing);
            Assert.False(grocery.IsDisabledByAnotherEdit);
            Assert.False(netflix.IsDisabledByAnotherEdit);
            Assert.Null(vm.EditingTransaction);
        });
    }

    [Fact]
    public void LedgerTagsForEditing_ExposeOnlyNonSystemTagsAndUpdateTransactionSelection()
    {
        RunInSta(() =>
        {
            var vm = CreateVm(unitOfWork: CreateUnitOfWork());
            vm.LoadAsync().GetAwaiter().GetResult();
            var grocery = GetItems(vm.TransactionsView).Single(item => item.Name == "FreshMart Grocery");

            Assert.Equal(new[] { "Groceries", "Streaming" }, vm.EditableTags.Select(tag => tag.Name).ToArray());
            Assert.DoesNotContain(vm.EditableTags, tag => tag.IsSystemTag);

            vm.EditTransactionCommand.ExecuteAsync(grocery).GetAwaiter().GetResult();
            vm.ApplyTransactionTag(grocery, vm.EditableTags.Single(tag => tag.Name == "Streaming"));

            Assert.Equal(2, grocery.TagId);
            Assert.Equal("Streaming", grocery.TagName);
            Assert.Equal("#D97936", grocery.TagHexCode);
        });
    }

    [Fact]
    public void RemoveTransactionCommand_RemovesIncomeAndRecordsMemoryAction()
    {
        RunInSta(() =>
        {
            var messenger = new WeakReferenceMessenger();
            var recipient = new MessageCaptureRecipient();
            messenger.Register<MessageCaptureRecipient, RecordLogMemoryMessage>(recipient,
                static (target, message) => target.RecordLogMemoryMessages.Add(message));
            var vm = CreateVm(unitOfWork: CreateUnitOfWork(), messenger: messenger);
            vm.LoadAsync().GetAwaiter().GetResult();
            var salary = GetItems(vm.TransactionsView).Single(item => item.Name == "June Salary");

            vm.RemoveTransactionCommand.ExecuteAsync(salary).GetAwaiter().GetResult();

            Assert.DoesNotContain(GetItems(vm.TransactionsView), item => item.Name == "June Salary");
            var action = Assert.IsType<DeleteIncomeLogMemoryAction>(Assert.Single(recipient.RecordLogMemoryMessages).Value);
            Assert.Equal(10, action.Snapshot.IncomeLogId);
        });
    }

    [Fact]
    public void EditAndRemoveCommands_DoNothingForGoalRows()
    {
        RunInSta(() =>
        {
            var messenger = new WeakReferenceMessenger();
            var recipient = new MessageCaptureRecipient();
            messenger.Register<MessageCaptureRecipient, RecordLogMemoryMessage>(recipient,
                static (target, message) => target.RecordLogMemoryMessages.Add(message));
            var vm = CreateVm(unitOfWork: CreateUnitOfWork(), messenger: messenger);
            vm.LoadAsync().GetAwaiter().GetResult();
            var goal = GetItems(vm.TransactionsView).Single(item => item.IsGoal);

            vm.EditTransactionCommand.ExecuteAsync(goal).GetAwaiter().GetResult();
            vm.RemoveTransactionCommand.ExecuteAsync(goal).GetAwaiter().GetResult();

            Assert.False(goal.IsEditing);
            Assert.Contains(GetItems(vm.TransactionsView), item => item.IsGoal);
            Assert.Empty(recipient.RecordLogMemoryMessages);
        });
    }

    [Fact]
    public void ExportCsv_UsesUtf8BomAndCurrentFilteredVisibleRows()
    {
        RunInSta(() =>
        {
            var vm = CreateVm();
            vm.LoadAsync().GetAwaiter().GetResult();
            vm.SearchText = "flix";

            var rows = GetVisibleExportRows(vm);
            var bytes = BuildExportBytes(rows);

            Assert.True(bytes.Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }));
            Assert.Equal(
                $"Date,Amount,Type,Tag{Environment.NewLine}2026-06-03,-15.99,Expense,Streaming{Environment.NewLine}",
                DecodeUtf8BomCsv(bytes));
        });
    }

    [Fact]
    public void ExportCsv_FormatsSignedAmountsTypesAndBlankIncomeTags()
    {
        var bytes = BuildExportBytes(
        [
            new LedgerTransactionItemVM
            {
                Kind = LedgerTransactionKind.Income,
                Amount = 2800m,
                OccurredOn = new DateTime(2026, 6, 3),
                TagName = "Ignored"
            },
            new LedgerTransactionItemVM
            {
                Kind = LedgerTransactionKind.Expense,
                Amount = 67.5m,
                OccurredOn = new DateTime(2026, 6, 4),
                TagName = string.Empty
            }
        ]);

        Assert.Equal(
            $"Date,Amount,Type,Tag{Environment.NewLine}2026-06-03,2800.00,Income,{Environment.NewLine}2026-06-04,-67.50,Expense,{Environment.NewLine}",
            DecodeUtf8BomCsv(bytes));
    }

    [Fact]
    public void ExportCsv_EscapesCsvFields()
    {
        var bytes = BuildExportBytes(
        [
            new LedgerTransactionItemVM
            {
                Kind = LedgerTransactionKind.Expense,
                Amount = 12.3m,
                OccurredOn = new DateTime(2026, 6, 5),
                TagName = "Food, \"Home\"\nNight"
            }
        ]);

        Assert.Equal(
            $"Date,Amount,Type,Tag{Environment.NewLine}2026-06-05,-12.30,Expense,\"Food, \"\"Home\"\"\nNight\"{Environment.NewLine}",
            DecodeUtf8BomCsv(bytes));
    }

    private static LedgerVM CreateVm(
        IExpenseLogService? expenseLogService = null,
        ISpendingSourceService? spendingSourceService = null,
        ITagService? tagService = null,
        IUnitOfWork? unitOfWork = null,
        IMessenger? messenger = null)
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
            messenger ?? new WeakReferenceMessenger());
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
        var expenseLog = CreateEditableExpenseLog();
        var expenseLogs = Substitute.For<IExpenseLogRepository>();
        expenseLogs.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(expenseLog);
        unitOfWork.ExpenseLogs.Returns(expenseLogs);

        var expenses = Substitute.For<IExpenseRepository>();
        unitOfWork.Expenses.Returns(expenses);

        var spendingSources = Substitute.For<ISpendingSourceRepository>();
        spendingSources.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(CreateCheckingSource());
        spendingSources.GetByIdAsync(2, Arg.Any<CancellationToken>())
            .Returns(CreateCreditSource());
        unitOfWork.SpendingSources.Returns(spendingSources);

        var tags = Substitute.For<IExpenseTagRepository>();
        tags.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(new ExpenseTag { Id = 1, Name = "Groceries", HexCode = "#53A96B" });
        tags.GetByIdAsync(2, Arg.Any<CancellationToken>())
            .Returns(new ExpenseTag { Id = 2, Name = "Streaming", HexCode = "#D97936" });
        unitOfWork.ExpenseTags.Returns(tags);

        var incomeLogs = Substitute.For<IIncomeLogRepository>();
        incomeLogs.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(CreateIncomeLogs());
        incomeLogs.GetByIdAsync(10, Arg.Any<CancellationToken>())
            .Returns(new IncomeLog
            {
                Id = 10,
                Name = "June Salary",
                Amount = 2800m,
                AddedOn = new DateTime(2026, 6, 3, 14, 0, 0),
                SpendingSourceId = 1,
                SpendingSource = CreateCheckingSource()
            });
        unitOfWork.IncomeLogs.Returns(incomeLogs);
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(1);
        return unitOfWork;
    }

    private static ExpenseLog CreateEditableExpenseLog()
    {
        var source = CreateCheckingSource();
        var tag = new ExpenseTag { Id = 1, Name = "Groceries", HexCode = "#53A96B" };
        var expense = new Expense
        {
            Id = 1,
            Name = "FreshMart Grocery",
            Amount = 67.50m,
            ExpenseCategory = ExpenseCategory.Needs,
            SpendingSourceId = source.Id,
            SpendingSource = source,
            ExpenseTagId = tag.Id,
            ExpenseTag = tag
        };

        return new ExpenseLog
        {
            Id = 1,
            ExpenseId = expense.Id,
            Expense = expense,
            Amount = expense.Amount,
            DeductedOn = new DateTime(2026, 6, 3, 9, 15, 0),
            Notes = string.Empty,
            SpendingSourceId = source.Id,
            SpendingSource = source
        };
    }

    private static SpendingSource CreateCheckingSource()
    {
        return new SpendingSource
        {
            Id = 1,
            Name = "DBS Checking",
            SpendingSourceType = SpendingSourceType.Checking,
            IsEnabled = true
        };
    }

    private static SpendingSource CreateCreditSource()
    {
        return new SpendingSource
        {
            Id = 2,
            Name = "Citibank Credit",
            SpendingSourceType = SpendingSourceType.Credit,
            IsEnabled = false
        };
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

    private static IReadOnlyList<LedgerTransactionItemVM> GetVisibleExportRows(LedgerVM vm)
    {
        return vm.GetVisibleTransactionsForExport();
    }

    private static byte[] BuildExportBytes(IReadOnlyList<LedgerTransactionItemVM> rows)
    {
        return LedgerCsvExport.BuildBytes(rows);
    }

    private static string DecodeUtf8BomCsv(byte[] bytes)
    {
        var preamble = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetPreamble();
        Assert.True(bytes.Take(preamble.Length).SequenceEqual(preamble));
        return Encoding.UTF8.GetString(bytes, preamble.Length, bytes.Length - preamble.Length);
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

    private sealed class MessageCaptureRecipient
    {
        public List<RecordLogMemoryMessage> RecordLogMemoryMessages { get; } = [];
    }
}
