using AutoMapper;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Constants;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Services.Persistence;
using Fluxo.Tests.TestDoubles;
using Fluxo.ViewModels.Popups.Settings;
using Fluxo.ViewModels.Shell;
using Fluxo.ViewModels.Shell.Main;
using NSubstitute;
using Xunit;

namespace Fluxo.Tests.ViewModels.Popups.Settings;

public sealed class SettingsIoUsTabVMTests
{
    [Fact]
    public async Task LoadAsync_ListsUnresolvedLendsAndDebts()
    {
        var account = new Account { Id = 10, Name = "Checking", AccountType = AccountType.Checking };
        var appData = CreateAppData(
            [
                new ExpenseLog
                {
                    Id = 1,
                    Amount = 25m,
                    DeductedOn = new DateTime(2026, 6, 20),
                    IsLend = true,
                    Account = account,
                    AccountId = account.Id,
                    Expense = new Expense { Id = 2, Name = "Lunch lend", IsLend = true }
                }
            ],
            [
                new IncomeLog
                {
                    Id = 3,
                    Name = "Advance",
                    Amount = 40m,
                    AddedOn = new DateTime(2026, 6, 20),
                    IsDebt = true,
                    Account = account,
                    AccountId = account.Id
                }
            ],
            [],
            [account]);
        var vm = CreateVm(appData);

        await vm.LoadAsync();

        Assert.Equal(2, vm.Items.Count);
        Assert.Contains(vm.Items, item => item.Kind == IoUKind.Lend && item.TransactionId == 1);
        Assert.Contains(vm.Items, item => item.Kind == IoUKind.Debt && item.TransactionId == 3);
    }

    [Fact]
    public async Task LoadAsync_UpdatesTotalAmountText()
    {
        var account = new Account { Id = 10, Name = "Checking", AccountType = AccountType.Checking };
        var appData = CreateAppData(
            [
                new ExpenseLog
                {
                    Id = 1,
                    Amount = 25m,
                    DeductedOn = new DateTime(2026, 6, 20),
                    IsLend = true,
                    Account = account,
                    AccountId = account.Id,
                    Expense = new Expense { Id = 2, Name = "Lunch lend", IsLend = true }
                }
            ],
            [
                new IncomeLog
                {
                    Id = 3,
                    Name = "Advance",
                    Amount = 40m,
                    AddedOn = new DateTime(2026, 6, 20),
                    IsDebt = true,
                    Account = account,
                    AccountId = account.Id
                }
            ],
            [],
            [account]);
        var vm = CreateVm(appData);

        await vm.LoadAsync();

        Assert.Equal("Total: 65", vm.TotalAmountText);
    }

    [Fact]
    public async Task ResolveAsync_LendCreatesIncomeAndClearsFlags()
    {
        var account = new Account
        {
            Id = 10,
            Name = "Checking",
            AccountType = AccountType.Checking,
            Balance = 100m
        };
        var expense = new Expense
        {
            Id = 2,
            Name = "Lunch lend",
            IsLend = true,
            ExpenseCategory = ExpenseCategory.Needs,
            Tag = new Tag { Id = 20, Name = "Food", HexCode = "#22C55E" }
        };
        var log = new ExpenseLog
        {
            Id = 1,
            Amount = 25m,
            IsLend = true,
            Account = account,
            AccountId = account.Id,
            Expense = expense,
            Notes = string.Empty
        };
        var appData = CreateAppData([log], [], [], [account]);
        var vm = CreateVm(appData);
        await vm.LoadAsync();

        var result = await vm.ResolveAsync(vm.Items.Single());

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.False(log.IsLend);
        Assert.False(expense.IsLend);
        Assert.Equal(125m, account.Balance);
        _ = appData.Received(1).AddIncomeLogAsync(
            Arg.Is<IncomeLog>(income =>
                income.Amount == 25m &&
                income.AccountId == 10 &&
                !income.IsDebt &&
                income.Name == "Lunch lend - IOU resolved"),
            Arg.Any<CancellationToken>());
        appData.Received(1).UpdateExpense(expense);
        appData.Received(1).UpdateExpenseLog(log);
        appData.Received(1).UpdateAccount(account);
    }

    [Fact]
    public async Task ResolveAsync_DebtCreatesBudgetReconciliationExpenseAndClearsFlag()
    {
        var account = new Account
        {
            Id = 10,
            Name = "Checking",
            AccountType = AccountType.Checking,
            Balance = 100m
        };
        var income = new IncomeLog
        {
            Id = 3,
            Name = "Advance",
            Amount = 40m,
            IsDebt = true,
            Account = account,
            AccountId = account.Id,
            Notes = string.Empty
        };
        var tags = new List<Tag>();
        var appData = CreateAppData([], [income], tags, [account]);
        var vm = CreateVm(appData);
        await vm.LoadAsync();

        var result = await vm.ResolveAsync(vm.Items.Single());

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.False(income.IsDebt);
        Assert.Equal(60m, account.Balance);
        _ = appData.Received(1).AddTagAsync(
            Arg.Is<Tag>(tag =>
                tag.Name == SystemTags.BudgetReconciliationName &&
                tag.HexCode == SystemTags.BudgetReconciliationHexCode &&
                tag.IsSystemTag),
            Arg.Any<CancellationToken>());
        _ = appData.Received(1).AddExpenseAsync(
            Arg.Is<Expense>(expense =>
                expense.Amount == 40m &&
                expense.ExpenseCategory == ExpenseCategory.Needs &&
                expense.Tag.Name == SystemTags.BudgetReconciliationName),
            Arg.Any<CancellationToken>());
        _ = appData.Received(1).AddExpenseLogAsync(
            Arg.Is<ExpenseLog>(log => log.Amount == 40m && log.AccountId == 10),
            Arg.Any<CancellationToken>());
        appData.Received(1).UpdateIncomeLog(income);
        appData.Received(1).UpdateAccount(account);
    }

    private static SettingsIoUsTabVM CreateVm(IAppDataService appData)
    {
        var messenger = new WeakReferenceMessenger();
        return new SettingsIoUsTabVM(
            CreateMainViewModel(messenger),
            appData,
            messenger,
            () => new DateTime(2026, 6, 20),
            () => Task.CompletedTask);
    }

    private static IAppDataService CreateAppData(
        List<ExpenseLog> expenseLogs,
        List<IncomeLog> incomeLogs,
        List<Tag> tags,
        List<Account> accounts)
    {
        var appData = Substitute.For<IAppDataService>();
        var nextId = 1000;

        appData.GetExpenseLogsAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<ExpenseLog>>(expenseLogs));
        appData.GetIncomeLogsAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<IncomeLog>>(incomeLogs));
        appData.GetTagsAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<Tag>>(tags));
        appData.GetExpenseLogByLogIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<ExpenseLog?>(expenseLogs.SingleOrDefault(log => log.Id == call.ArgAt<int>(0))));
        appData.GetIncomeLogByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<IncomeLog?>(incomeLogs.SingleOrDefault(log => log.Id == call.ArgAt<int>(0))));
        appData.GetAccountByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<Account?>(accounts.SingleOrDefault(account => account.Id == call.ArgAt<int>(0))));

        appData.AddIncomeLogAsync(Arg.Any<IncomeLog>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var log = call.ArgAt<IncomeLog>(0);
                if (log.Id <= 0)
                    log.Id = nextId++;
                incomeLogs.Add(log);
                return Task.CompletedTask;
            });
        appData.AddTagAsync(Arg.Any<Tag>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var tag = call.ArgAt<Tag>(0);
                if (tag.Id <= 0)
                    tag.Id = nextId++;
                tags.Add(tag);
                return Task.CompletedTask;
            });
        appData.AddExpenseAsync(Arg.Any<Expense>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var expense = call.ArgAt<Expense>(0);
                if (expense.Id <= 0)
                    expense.Id = nextId++;
                return Task.CompletedTask;
            });
        appData.AddExpenseLogAsync(Arg.Any<ExpenseLog>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var log = call.ArgAt<ExpenseLog>(0);
                if (log.Id <= 0)
                    log.Id = nextId++;
                expenseLogs.Add(log);
                return Task.CompletedTask;
            });
        appData.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        return appData;
    }

    private static MainVM CreateMainViewModel(IMessenger messenger)
    {
        var mapper = Substitute.For<IMapper>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var userSettings = Substitute.For<IUserSettingsRepository>();
        userSettings.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<UserSettings>>([]));
        unitOfWork.UserSettings.Returns(userSettings);
        var incomeLogs = Substitute.For<IIncomeLogRepository>();
        incomeLogs.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<IncomeLog>>([]));
        unitOfWork.IncomeLogs.Returns(incomeLogs);

        var runner = new InlineDataOperationRunner(unitOfWork);
        var dashboard = new DashboardVM(
            new NotificationPanelVM(
                Substitute.For<IExpenseService>(),
                Substitute.For<IExpenseLogService>(),
                Substitute.For<IAccountService>(),
                runner,
                mapper,
                messenger: messenger),
            new BudgetAllocationPanelVM(
                Substitute.For<IExpenseLogService>(),
                Substitute.For<IAccountService>(),
                Substitute.For<ITagService>(),
                runner,
                mapper,
                messenger),
            new SpentAllowancePanelVM(
                Substitute.For<IExpenseLogService>(),
                Substitute.For<IAccountService>(),
                runner,
                mapper,
                messenger),
            new SavingGoalsPanelVM(runner, mapper, messenger),
            new UpcomingEventsPanelVM(runner, mapper, messenger: messenger),
            new MainViewModeToggleVM(messenger));

        return new MainVM(
            runner,
            dashboard,
            new DaySpinnerVM(messenger),
            null);
    }
}
