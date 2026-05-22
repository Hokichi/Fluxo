using AutoMapper;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Constants;
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
        var source = new SpendingSourceVM
        {
            Id = 1,
            Name = "Checking",
            SpendingSourceType = SpendingSourceType.Checking,
            Balance = 1000m,
            IsEnabled = true
        };
        var expenseLog = new ExpenseLogVM
        {
            Id = 10,
            Amount = 100m,
            DeductedOn = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 10),
            Notes = "groceries",
            SpendingSource = source,
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
        Assert.Equal(23.33m, vm.Allowance);

        messenger.Send(new RecordLogMemoryMessage(new DeleteExpenseLogMemoryAction(
            new ExpenseLogMemorySnapshot(
                ExpenseId: 20,
                ExpenseLogId: 10,
                ExpenseName: "Groceries",
                Amount: 100m,
                ExpenseCategory: ExpenseCategory.Needs,
                SpendingSourceId: 1,
                TagId: 0,
                DeductedOn: expenseLog.DeductedOn,
                Notes: "groceries",
                IsForDeletion: false))));

        Assert.Equal(0m, vm.TotalSpent);
        Assert.Equal(29.33m, vm.Allowance);
    }

    private static SpentAllowancePanelVM CreateVm(
        IMessenger messenger,
        IReadOnlyList<ExpenseLogVM> expenseLogs,
        IReadOnlyList<SpendingSourceVM> spendingSources)
    {
        var expenseLogService = Substitute.For<IExpenseLogService>();
        expenseLogService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ExpenseLogDto>>([]));

        var spendingSourceService = Substitute.For<ISpendingSourceService>();
        spendingSourceService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<SpendingSourceDto>>([]));

        var userSettingsRepository = Substitute.For<Fluxo.Core.Interfaces.Repositories.IUserSettingsRepository>();
        userSettingsRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<UserSettings>>(
            [
                new UserSettings { Name = UserSettingNames.NeedsThreshold, Value = "50" },
                new UserSettings { Name = UserSettingNames.WantsThreshold, Value = "30" }
            ]));
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.UserSettings.Returns(userSettingsRepository);
        var dataOperationRunner = new InlineDataOperationRunner(unitOfWork);

        var mapper = Substitute.For<IMapper>();
        mapper.Map<IReadOnlyList<ExpenseLogVM>>(Arg.Any<object>()).Returns(expenseLogs);
        mapper.Map<IReadOnlyList<SpendingSourceVM>>(Arg.Any<object>()).Returns(spendingSources);

        return new SpentAllowancePanelVM(
            expenseLogService,
            spendingSourceService,
            dataOperationRunner,
            mapper,
            messenger);
    }
}
