using AutoMapper;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.DTO;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Mappings;
using Fluxo.Resources.Resources.Messages;
using Fluxo.ViewModels.Shell.Main;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Fluxo.Tests.ViewModels.Shell.Main;

public sealed class LedgerVMDateRangeTests
{
    [Fact]
    public async Task LoadAllTransactionsAsync_SetsEarliestDateThroughToday()
    {
        var vm = CreateVm(
        [
            new TransactionDto { Id = 1, Type = TransactionType.Expense, Name = "Old", OccurredOn = DateTime.Today.AddDays(-20), LoggedOn = DateTime.Today.AddDays(-20) },
            new TransactionDto { Id = 2, Type = TransactionType.Income, Name = "New", OccurredOn = DateTime.Today.AddDays(-2), LoggedOn = DateTime.Today.AddDays(-2) }
        ]);

        await vm.LoadAllTransactionsAsync();

        Assert.Equal(DateTime.Today.AddDays(-20), vm.StartDate.Date);
        Assert.Equal(DateTime.Today, vm.EndDate.Date);
    }

    [Fact]
    public async Task LoadAllTransactionsAsync_WithoutTransactions_UsesToday()
    {
        var vm = CreateVm([]);

        await vm.LoadAllTransactionsAsync();

        Assert.Equal(DateTime.Today, vm.StartDate.Date);
        Assert.Equal(DateTime.Today, vm.EndDate.Date);
    }

    [Fact]
    public void GlobalDashboardPeriodMessages_DoNotReplaceLedgerSelection()
    {
        var messenger = new WeakReferenceMessenger();
        var vm = CreateVm([], messenger);
        vm.ApplyExternalDateRange(DateTime.Today.AddDays(-10), DateTime.Today.AddDays(-3), refresh: false);

        messenger.Send(new DateRangeSelectionChangedMessage(DateTime.Today.AddDays(-2), DateTime.Today));
        messenger.Send(new AllTimeViewModeMessage());

        Assert.Equal(DateTime.Today.AddDays(-10), vm.StartDate.Date);
        Assert.Equal(DateTime.Today.AddDays(-3), vm.EndDate.Date);
    }

    [Fact]
    public async Task DedicatedDateRangeMessage_AppliesRangeOnNextLoad()
    {
        var messenger = new WeakReferenceMessenger();
        var vm = CreateVm([], messenger);

        messenger.Send(new LedgerDateRangeRequestedMessage(DateTime.Today.AddDays(-6), DateTime.Today));
        await vm.LoadAsync();

        Assert.Equal(DateTime.Today.AddDays(-6), vm.StartDate.Date);
        Assert.Equal(DateTime.Today, vm.EndDate.Date);
    }

    [Fact]
    public async Task DedicatedAllTimeMessage_RunsAllTransactionsFlowOnNextLoad()
    {
        var messenger = new WeakReferenceMessenger();
        var vm = CreateVm(
        [
            new TransactionDto { Id = 1, Type = TransactionType.Expense, Name = "Old", OccurredOn = DateTime.Today.AddDays(-20), LoggedOn = DateTime.Today.AddDays(-20) }
        ], messenger);

        messenger.Send(new LedgerAllTimeRequestedMessage());
        await vm.LoadAsync();

        Assert.Equal(DateTime.Today.AddDays(-20), vm.StartDate.Date);
        Assert.Equal(DateTime.Today, vm.EndDate.Date);
    }

    [Fact]
    public async Task CategoryFilter_Excluded_ShowsOnlyExcludedTransactions()
    {
        var vm = CreateVm(CreateCategoryFilterTransactions());
        await vm.LoadAsync();

        vm.CategoryFilters.Single(option => option.IsAll).IsChecked = false;
        vm.CategoryFilters.Single(option => option.Label == "Excluded").IsChecked = true;
        vm.TransactionsView.Refresh();

        Assert.Equal(
            ["Excluded expense", "Excluded income"],
            vm.TransactionsView.Cast<LedgerTransactionItemVM>().Select(item => item.Name).Order());
    }

    [Fact]
    public async Task CategoryFilter_NeedsAndExcluded_UsesUnion()
    {
        var vm = CreateVm(CreateCategoryFilterTransactions());
        await vm.LoadAsync();

        vm.CategoryFilters.Single(option => option.IsAll).IsChecked = false;
        vm.CategoryFilters.Single(option => option.Label == "Needs").IsChecked = true;
        vm.CategoryFilters.Single(option => option.Label == "Excluded").IsChecked = true;
        vm.TransactionsView.Refresh();

        Assert.Equal(
            ["Excluded expense", "Excluded income", "Needs expense"],
            vm.TransactionsView.Cast<LedgerTransactionItemVM>().Select(item => item.Name).Order());
    }

    [Fact]
    public async Task LoadAsync_OrdersTransactionsByLoggedOn()
    {
        var vm = CreateVm(
        [
            new TransactionDto { Id = 1, Type = TransactionType.Expense, Name = "Older", Amount = 1m, OccurredOn = DateTime.Today, LoggedOn = DateTime.Today.AddHours(8) },
            new TransactionDto { Id = 2, Type = TransactionType.Expense, Name = "Newer", Amount = 100m, OccurredOn = DateTime.Today, LoggedOn = DateTime.Today.AddHours(9) }
        ]);

        await vm.LoadAsync();

        Assert.Equal(["Newer", "Older"], vm.TransactionsView.Cast<LedgerTransactionItemVM>().Select(item => item.Name));
    }

    [Fact]
    public async Task LoadAsync_InitiallyShowsOnlyTodayTransactions()
    {
        var vm = CreateVm(
        [
            new TransactionDto { Id = 1, Type = TransactionType.Expense, Name = "Older", OccurredOn = DateTime.Today.AddDays(-1), LoggedOn = DateTime.Today.AddDays(-1) },
            new TransactionDto { Id = 2, Type = TransactionType.Income, Name = "Today", OccurredOn = DateTime.Today, LoggedOn = DateTime.Today }
        ]);

        await vm.LoadAsync();

        Assert.Equal(["Today"], vm.TransactionsView.Cast<LedgerTransactionItemVM>().Select(item => item.Name));
    }

    private static IReadOnlyList<TransactionDto> CreateCategoryFilterTransactions() =>
    [
        new TransactionDto { Id = 1, Type = TransactionType.Expense, Name = "Needs expense", ExpenseCategory = ExpenseCategory.Needs, OccurredOn = DateTime.Today, LoggedOn = DateTime.Today },
        new TransactionDto { Id = 2, Type = TransactionType.Expense, Name = "Excluded expense", ExpenseCategory = ExpenseCategory.Wants, IsExcludedFromBudget = true, OccurredOn = DateTime.Today, LoggedOn = DateTime.Today },
        new TransactionDto { Id = 3, Type = TransactionType.Income, Name = "Excluded income", IsExcludedFromBudget = true, OccurredOn = DateTime.Today, LoggedOn = DateTime.Today },
        new TransactionDto { Id = 4, Type = TransactionType.Income, Name = "Included income", OccurredOn = DateTime.Today, LoggedOn = DateTime.Today }
    ];

    private static LedgerVM CreateVm(IReadOnlyList<TransactionDto> transactions, IMessenger? messenger = null)
    {
        var transactionService = Substitute.For<ITransactionService>();
        transactionService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(transactions);
        var accountService = Substitute.For<IAccountService>();
        accountService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([]);
        var tagService = Substitute.For<ITagService>();
        tagService.GetAllAsync(Arg.Any<CancellationToken>()).Returns([]);
        var mapper = new MapperConfiguration(
            configuration => configuration.AddProfile<DtoViewModelProfile>(),
            NullLoggerFactory.Instance).CreateMapper();

        return new LedgerVM(
            transactionService,
            accountService,
            tagService,
            Substitute.For<IDataOperationRunner>(),
            mapper,
            messenger);
    }
}
