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
