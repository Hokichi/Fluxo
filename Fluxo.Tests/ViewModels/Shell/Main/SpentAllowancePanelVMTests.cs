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
using Fluxo.ViewModels.Shell.Main;
using NSubstitute;
using Xunit;

namespace Fluxo.Tests.ViewModels.Shell.Main;

public sealed class SpentAllowancePanelVMTests
{
    [Fact]
    public async Task RecordLogMemoryMessage_DeleteExpenseRestoresSpentAllowanceAndSourceTotalsWithoutReload()
    {
        var messenger = new WeakReferenceMessenger();
        var source = new AccountVM
        {
            Id = 1,
            Name = "Checking",
            AccountType = AccountType.Checking,
            Balance = 1000m,
            IsEnabled = true
        };
        var expenseLog = new ExpenseLogVM
        {
            Id = 10,
            Amount = 100m,
            DeductedOn = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 10),
            Notes = "groceries",
            Account = source,
            Expense = new ExpenseVM
            {
                Id = 20,
                Name = "Groceries",
                Amount = 100m,
                ExpenseCategory = ExpenseCategory.Needs
            }
        };
        var vm = CreateVm(messenger, [expenseLog], [source]);
        await vm.LoadAsync();

        Assert.Equal(100m, vm.TotalSpent);
        Assert.Equal(33.33m, vm.Allowance);

        messenger.Send(new RecordLogMemoryMessage(new DeleteExpenseLogMemoryAction(
            new ExpenseLogMemorySnapshot(
                ExpenseId: 20,
                ExpenseLogId: 10,
                ExpenseName: "Groceries",
                Amount: 100m,
                ExpenseCategory: ExpenseCategory.Needs,
                AccountId: 1,
                TagId: 0,
                DeductedOn: expenseLog.DeductedOn,
                Notes: "groceries",
                IsForDeletion: false,
                ParentLogId: null))));

        Assert.Equal(0m, vm.TotalSpent);
        Assert.Equal(36.67m, vm.Allowance);
    }

    [Fact]
    public async Task LoadAsync_DailyAllowanceUsesAllocationLimitAndPeriod()
    {
        var messenger = new WeakReferenceMessenger();
        var vm = CreateVm(
            messenger,
            [],
            [CreateCheckingSource(balance: 1_000m)],
            new BudgetAllocation
            {
                AllocationLimit = 280m,
                AllocationPeriod = AllocationPeriod.Monthly
            },
            () => new DateTime(2026, 2, 10));

        await vm.LoadAsync();

        Assert.Equal(10m, vm.Allowance);
    }

    [Fact]
    public async Task DateRangeSelectionChanged_NetUsesIncomeAndExpensesForSelectedDay()
    {
        var messenger = new WeakReferenceMessenger();
        var selectedDay = new DateTime(2026, 6, 12);
        var vm = CreateVm(
            messenger,
            [
                CreateExpenseLog(1, 75m, selectedDay),
                CreateExpenseLog(2, 40m, selectedDay.AddDays(-1))
            ],
            [CreateCheckingSource(balance: 1_000m)],
            incomeLogs:
            [
                CreateIncomeLog(1, 125m, selectedDay),
                CreateIncomeLog(2, 20m, selectedDay.AddDays(1))
            ]);

        await vm.LoadAsync();

        messenger.Send(new DateRangeSelectionChangedMessage(selectedDay, selectedDay));

        Assert.Equal(75m, vm.TotalSpent);
        Assert.Equal(125m, vm.TotalEarned);
        Assert.Equal(50m, vm.Net);
    }

    [Fact]
    public async Task LoadAsync_TotalSpent_ExcludesSplitParentLogsAndIncludesChildExpenseLogs()
    {
        var messenger = new WeakReferenceMessenger();
        var selectedDay = new DateTime(2026, 6, 12);
        var vm = CreateVm(
            messenger,
            [
                CreateExpenseLog(1, 100m, selectedDay),
                CreateExpenseLog(2, 40m, selectedDay, parentLogId: 1)
            ],
            [CreateCheckingSource(balance: 1_000m)]);

        await vm.LoadAsync();

        Assert.Equal(40m, vm.TotalSpent);
    }

    private static SpentAllowancePanelVM CreateVm(
        IMessenger messenger,
        IReadOnlyList<ExpenseLogVM> expenseLogs,
        IReadOnlyList<AccountVM> accounts,
        BudgetAllocation? budgetAllocation = null,
        Func<DateTime>? todayProvider = null,
        IReadOnlyList<IncomeLog>? incomeLogs = null)
    {
        var expenseLogService = Substitute.For<IExpenseLogService>();
        expenseLogService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ExpenseLogDto>>([]));

        var accountService = Substitute.For<IAccountService>();
        accountService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<AccountDto>>([]));

        var userSettingsRepository = Substitute.For<Fluxo.Core.Interfaces.Repositories.IUserSettingsRepository>();
        userSettingsRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<UserSettings>>(
            [
                new UserSettings { Name = "NeedsThreshold", Value = "50" },
                new UserSettings { Name = "WantsThreshold", Value = "30" }
            ]));
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.UserSettings.Returns(userSettingsRepository);
        var budgetAllocationRepository = Substitute.For<Fluxo.Core.Interfaces.Repositories.IBudgetAllocationRepository>();
        budgetAllocationRepository.GetAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<BudgetAllocation?>(budgetAllocation ?? new BudgetAllocation()));
        unitOfWork.BudgetAllocation.Returns(budgetAllocationRepository);
        var incomeLogRepository = Substitute.For<Fluxo.Core.Interfaces.Repositories.IIncomeLogRepository>();
        incomeLogRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(incomeLogs ?? []));
        unitOfWork.IncomeLogs.Returns(incomeLogRepository);
        var dataOperationRunner = new InlineDataOperationRunner(unitOfWork);

        var mapper = Substitute.For<IMapper>();
        mapper.Map<IReadOnlyList<ExpenseLogVM>>(Arg.Any<object>()).Returns(expenseLogs);
        mapper.Map<IReadOnlyList<AccountVM>>(Arg.Any<object>()).Returns(accounts);

        return new SpentAllowancePanelVM(
            expenseLogService,
            accountService,
            dataOperationRunner,
            mapper,
            messenger,
            todayProvider);
    }

    private static AccountVM CreateCheckingSource(decimal balance)
    {
        return new AccountVM
        {
            Id = 1,
            Name = "Checking",
            AccountType = AccountType.Checking,
            Balance = balance,
            IsEnabled = true
        };
    }

    private static ExpenseLogVM CreateExpenseLog(int id, decimal amount, DateTime deductedOn, int? parentLogId = null)
    {
        return new ExpenseLogVM
        {
            Id = id,
            ParentLogId = parentLogId,
            Amount = amount,
            DeductedOn = deductedOn,
            Expense = new ExpenseVM
            {
                Id = id,
                Name = $"Expense {id}",
                Amount = amount,
                ExpenseCategory = ExpenseCategory.Needs
            },
            Account = CreateCheckingSource(1_000m)
        };
    }

    private static IncomeLog CreateIncomeLog(int id, decimal amount, DateTime addedOn)
    {
        return new IncomeLog
        {
            Id = id,
            Name = $"Income {id}",
            Amount = amount,
            AddedOn = addedOn,
            Notes = string.Empty,
            AccountId = 1,
            Account = new Account()
        };
    }
}
