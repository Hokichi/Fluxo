using System.Runtime.ExceptionServices;
using AutoMapper;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Tests.TestDoubles;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Popups;
using Fluxo.ViewModels.Shell.Main;
using NSubstitute;
using Xunit;

namespace Fluxo.Tests.ViewModels.Popups;

public sealed class IncomeDetailVMTests
{
    [Fact]
    public void IncomeDetailVM_DoesNotExposeInlineEditingApi()
    {
        var type = typeof(IncomeDetailVM);

        Assert.Null(type.GetProperty("IsEditing"));
        Assert.Null(type.GetProperty("AreFieldsReadOnly"));
        Assert.Null(type.GetProperty("CanEditFields"));
        Assert.Null(type.GetMethod("BeginEditingAsync"));
        Assert.Null(type.GetMethod("CancelEditing"));
        Assert.Null(type.GetMethod("SaveAsync"));
        Assert.Null(type.GetMethod("HasValidChangesToPersistOnClose"));
    }

    [Fact]
    public async Task Constructor_AllowsNullNameAndNotes()
    {
        await RunInStaAsync(() =>
        {
            var exception = Record.Exception(() => CreateVm(name: null!, notes: null!));

            Assert.Null(exception);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Constructor_IncludesDisabledCurrentSourceForReadOnlyDisplay()
    {
        await RunInStaAsync(() =>
        {
            var disabledSource = CreateAccount(
                AccountType.Checking,
                balance: 1_000m,
                spentAmount: 0m,
                id: 1,
                isEnabled: false);
            var enabledSource = CreateAccount(
                AccountType.Checking,
                balance: 500m,
                spentAmount: 0m,
                id: 2,
                name: "Enabled source");
            var vm = CreateVm([disabledSource, enabledSource], disabledSource);

            Assert.Contains(vm.Accounts, source => source.Id == disabledSource.Id);
            Assert.Equal(disabledSource.Id, vm.SelectedAccount?.Id);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task CreateAddNewTransactionDraft_UsesDisplayedIncomeValues()
    {
        await RunInStaAsync(() =>
        {
            var (vm, source) = CreateVm(
                sourceBalance: 1_000m,
                incomeAmount: 125m,
                name: "Salary",
                notes: "Monthly salary");

            var draft = vm.CreateAddNewTransactionDraft();

            Assert.False(draft.IsExpense);
            Assert.Equal("Salary", draft.Name);
            Assert.Equal(125m, draft.AmountText);
            Assert.Equal(source.Id, draft.AccountId);
            Assert.Equal(new DateTime(2026, 5, 1), draft.Date);
            Assert.Equal("Monthly salary", draft.Note);
            Assert.Null(draft.Category);
            Assert.Null(draft.TagId);
            return Task.CompletedTask;
        });
    }

    private static (IncomeDetailVM Vm, AccountVM Source) CreateVm(
        decimal sourceBalance = 1_000m,
        decimal incomeAmount = 100m,
        AccountType sourceType = AccountType.Checking,
        decimal spentAmount = 0m,
        string? name = "Salary",
        string? notes = "Monthly salary")
    {
        var sourceVm = CreateAccount(sourceType, sourceBalance, spentAmount);
        return CreateVmWithSource([sourceVm], sourceVm, incomeAmount, name, notes);
    }

    private static IncomeDetailVM CreateVm(
        IReadOnlyList<AccountVM> accounts,
        AccountVM incomeSource,
        decimal incomeAmount = 100m,
        string? name = "Salary",
        string? notes = "Monthly salary")
    {
        return CreateVmWithSource(accounts, incomeSource, incomeAmount, name, notes).Vm;
    }

    private static (IncomeDetailVM Vm, AccountVM Source) CreateVmWithSource(
        IReadOnlyList<AccountVM> accounts,
        AccountVM incomeSource,
        decimal incomeAmount,
        string? name,
        string? notes)
    {
        var main = CreateMainViewModel(accounts);
        var incomeLog = new IncomeLogVM
        {
            Id = 1,
            Name = name!,
            Amount = incomeAmount,
            AddedOn = new DateTime(2026, 5, 1),
            Notes = notes!,
            Account = incomeSource
        };

        return (new IncomeDetailVM(main, incomeLog, Substitute.For<IAppDataService>()), incomeSource);
    }

    private static MainVM CreateMainViewModel(IReadOnlyList<AccountVM> accounts)
    {
        var messenger = new WeakReferenceMessenger();
        var mapper = Substitute.For<IMapper>();
        var unitOfWork = CreateUnitOfWork();
        var dataOperationRunner = new InlineDataOperationRunner(unitOfWork);

        var dashboard = new DashboardVM(
            new NotificationPanelVM(
                Substitute.For<IExpenseService>(),
                Substitute.For<IExpenseLogService>(),
                Substitute.For<IAccountService>(),
                dataOperationRunner,
                mapper,
                messenger: messenger),
            new BudgetAllocationPanelVM(
                Substitute.For<IExpenseLogService>(),
                Substitute.For<IAccountService>(),
                Substitute.For<ITagService>(),
                dataOperationRunner,
                mapper,
                messenger),
            new SpentAllowancePanelVM(
                Substitute.For<ITransactionService>(),
                Substitute.For<IAccountService>(),
                dataOperationRunner,
                mapper,
                messenger),
            new SavingGoalsPanelVM(dataOperationRunner, mapper, messenger),
            new UpcomingEventsPanelVM(dataOperationRunner, mapper, messenger: messenger),
            new MainViewModeToggleVM(messenger));
        var main = new MainVM(
            dataOperationRunner,
            dashboard,
            new DaySpinnerVM(messenger),
            null);

        foreach (var source in accounts)
            main.BudgetPanel.Accounts.Add(source);

        return main;
    }

    private static IUnitOfWork CreateUnitOfWork()
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var userSettings = Substitute.For<IUserSettingsRepository>();
        userSettings.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<UserSettings>>([]));
        unitOfWork.UserSettings.Returns(userSettings);

        var incomeLogs = Substitute.For<IIncomeLogRepository>();
        incomeLogs.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<IncomeLog>>([]));
        unitOfWork.IncomeLogs.Returns(incomeLogs);

        return unitOfWork;
    }

    private static AccountVM CreateAccount(
        AccountType sourceType,
        decimal balance,
        decimal spentAmount,
        int id = 1,
        bool isEnabled = true,
        string name = "Source")
    {
        return new AccountVM
        {
            Id = id,
            Name = name,
            AccountType = sourceType,
            Balance = balance,
            SpentAmount = spentAmount,
            IsEnabled = isEnabled
        };
    }

    private static async Task RunInStaAsync(Func<Task> action)
    {
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                action().GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();

        await Task.CompletedTask;
    }
}
