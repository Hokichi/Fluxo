using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
using AutoMapper;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Services.Persistence;
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
    public async Task SaveAsync_ReturnsFailure_WhenAmountIsNotPositive()
    {
        await RunInStaAsync(async () =>
        {
            var (vm, _) = CreateVm();
            await vm.BeginEditingAsync();
            vm.AmountText = 0m;

            var result = await vm.SaveAsync();

            Assert.False(result.IsSuccess);
            Assert.Equal("Please enter a valid amount greater than zero.", result.ErrorMessage);
        });
    }

    [Fact]
    public async Task SaveAsync_UpdatesIncomeAndSourceBalances()
    {
        await RunInStaAsync(async () =>
        {
            var (vm, appData) = CreateVm();
            await vm.BeginEditingAsync();
            vm.NameText = "Bonus";
            vm.AmountText = 300m;
            vm.NoteText = "May bonus";
            vm.SelectedDate = new DateTime(2026, 5, 24);

            var result = await vm.SaveAsync();

            Assert.True(result.IsSuccess);
            appData.Received().UpdateIncomeLog(Arg.Is<IncomeLog>(log =>
                log.Name == "Bonus" &&
                log.Amount == 300m &&
                log.AddedOn == new DateTime(2026, 5, 24) &&
                log.Notes == "May bonus"));
            appData.Received().UpdateSpendingSource(Arg.Is<SpendingSource>(source =>
                source.Id == 1 &&
                source.Balance == 1_200m));
            await appData.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task SaveAsync_ReversesOldNormalSourceIncomeWithoutClampingBalance()
    {
        await RunInStaAsync(async () =>
        {
            var (vm, appData) = CreateVm(sourceBalance: 20m, incomeAmount: 100m);
            await vm.BeginEditingAsync();
            vm.AmountText = 50m;

            var result = await vm.SaveAsync();

            Assert.True(result.IsSuccess);
            appData.Received().UpdateSpendingSource(Arg.Is<SpendingSource>(source =>
                source.Id == 1 &&
                source.Balance == -30m));
        });
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
    public async Task SaveAsync_RestoresCreditSpentAmountBeforeApplyingEditedIncome()
    {
        await RunInStaAsync(async () =>
        {
            var (vm, appData) = CreateVm(
                sourceType: SpendingSourceType.Credit,
                sourceBalance: 0m,
                spentAmount: 0m,
                incomeAmount: 100m);
            await vm.BeginEditingAsync();
            vm.AmountText = 80m;

            var result = await vm.SaveAsync();

            Assert.True(result.IsSuccess);
            appData.Received().UpdateSpendingSource(Arg.Is<SpendingSource>(source =>
                source.Id == 1 &&
                source.SpendingSourceType == SpendingSourceType.Credit &&
                source.SpentAmount == 20m));
        });
    }

    [Fact]
    public async Task SaveAsync_PreservesDisabledCurrentSource_WhenSavingNameAndNoteEdit()
    {
        await RunInStaAsync(async () =>
        {
            var disabledSource = CreateSpendingSource(
                SpendingSourceType.Checking,
                balance: 1_000m,
                spentAmount: 0m,
                id: 1,
                isEnabled: false);
            var enabledSource = CreateSpendingSource(
                SpendingSourceType.Checking,
                balance: 500m,
                spentAmount: 0m,
                id: 2,
                name: "Enabled source");
            var main = CreateMainViewModel([disabledSource, enabledSource]);
            var appData = CreateAppData(
                [
                    CreatePersistedSource(disabledSource),
                    CreatePersistedSource(enabledSource)
                ],
                incomeSourceId: disabledSource.Id,
                incomeAmount: 100m,
                name: "Salary",
                notes: "Monthly salary");
            var incomeLog = new IncomeLogVM
            {
                Id = 1,
                Name = "Salary",
                Amount = 100m,
                AddedOn = new DateTime(2026, 5, 1),
                Notes = "Monthly salary",
                SpendingSource = disabledSource
            };
            var vm = new IncomeDetailVM(main, incomeLog, appData);

            await vm.BeginEditingAsync();
            vm.NameText = "Updated salary";
            vm.NoteText = "Updated note";

            var result = await vm.SaveAsync();

            Assert.True(result.IsSuccess);
            appData.Received().UpdateIncomeLog(Arg.Is<IncomeLog>(log =>
                log.Name == "Updated salary" &&
                log.Notes == "Updated note" &&
                log.SpendingSourceId == disabledSource.Id &&
                log.SpendingSource!.Id == disabledSource.Id));
            appData.DidNotReceive().UpdateSpendingSource(Arg.Is<SpendingSource>(source => source.Id == enabledSource.Id));
        });
    }

    private static (IncomeDetailVM Vm, IAppDataService AppData) CreateVm(
        decimal sourceBalance = 1_000m,
        decimal incomeAmount = 100m,
        SpendingSourceType sourceType = SpendingSourceType.Checking,
        decimal spentAmount = 0m,
        string? name = "Salary",
        string? notes = "Monthly salary")
    {
        var sourceVm = CreateSpendingSource(sourceType, sourceBalance, spentAmount);
        var main = CreateMainViewModel([sourceVm]);
        var appData = CreateAppData(sourceType, sourceBalance, spentAmount, incomeAmount, name, notes);
        var incomeLog = new IncomeLogVM
        {
            Id = 1,
            Name = name!,
            Amount = incomeAmount,
            AddedOn = new DateTime(2026, 5, 1),
            Notes = notes!,
            SpendingSource = sourceVm
        };

        return (new IncomeDetailVM(main, incomeLog, appData), appData);
    }

    private static IAppDataService CreateAppData(
        SpendingSourceType sourceType,
        decimal sourceBalance,
        decimal spentAmount,
        decimal incomeAmount,
        string? name,
        string? notes)
    {
        var persistedSource = new SpendingSource
        {
            Id = 1,
            Name = "Source",
            SpendingSourceType = sourceType,
            Balance = sourceBalance,
            SpentAmount = spentAmount,
            IsEnabled = true
        };
        return CreateAppData([persistedSource], persistedSource.Id, incomeAmount, name, notes);
    }

    private static IAppDataService CreateAppData(
        IReadOnlyList<SpendingSource> persistedSources,
        int incomeSourceId,
        decimal incomeAmount,
        string? name,
        string? notes)
    {
        var persistedSource = persistedSources.Single(source => source.Id == incomeSourceId);
        var persistedIncome = new IncomeLog
        {
            Id = 1,
            Name = name!,
            Amount = incomeAmount,
            AddedOn = new DateTime(2026, 5, 1),
            Notes = notes!,
            SpendingSourceId = persistedSource.Id,
            SpendingSource = persistedSource
        };

        var appData = Substitute.For<IAppDataService>();
        appData.GetIncomeLogByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IncomeLog?>(persistedIncome));

        foreach (var source in persistedSources)
        {
            appData.GetSpendingSourceByIdAsync(source.Id, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<SpendingSource?>(source));
        }

        appData.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        return appData;
    }

    private static SpendingSource CreatePersistedSource(SpendingSourceVM source)
    {
        return new SpendingSource
        {
            Id = source.Id,
            Name = source.Name,
            SpendingSourceType = source.SpendingSourceType,
            Balance = source.Balance,
            SpentAmount = source.SpentAmount,
            IsEnabled = source.IsEnabled
        };
    }

    private static MainVM CreateMainViewModel(IReadOnlyList<SpendingSourceVM> spendingSources)
    {
        var messenger = new WeakReferenceMessenger();
        var mapper = Substitute.For<IMapper>();
        var unitOfWork = CreateUnitOfWork();
        var dataOperationRunner = new InlineDataOperationRunner(unitOfWork);

        var main = new MainVM(
            dataOperationRunner,
            new NotificationPanelVM(
                Substitute.For<IExpenseService>(),
                Substitute.For<IExpenseLogService>(),
                Substitute.For<ISpendingSourceService>(),
                dataOperationRunner,
                mapper,
                messenger: messenger),
            new BudgetAllocationPanelVM(
                Substitute.For<IExpenseLogService>(),
                Substitute.For<ISpendingSourceService>(),
                Substitute.For<ITagService>(),
                dataOperationRunner,
                mapper,
                messenger),
            new SpentAllowancePanelVM(
                Substitute.For<IExpenseLogService>(),
                Substitute.For<ISpendingSourceService>(),
                dataOperationRunner,
                mapper,
                messenger),
            new SavingGoalsPanelVM(dataOperationRunner, mapper, messenger),
            new DaySpinnerVM(messenger),
            new MainViewModeToggleVM(messenger));

        foreach (var source in spendingSources)
            main.BudgetPanel.SpendingSources.Add(source);

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

    private static SpendingSourceVM CreateSpendingSource(
        SpendingSourceType sourceType,
        decimal balance,
        decimal spentAmount,
        int id = 1,
        bool isEnabled = true,
        string name = "Source")
    {
        return new SpendingSourceVM
        {
            Id = id,
            Name = name,
            SpendingSourceType = sourceType,
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
