using AutoMapper;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.DTO;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Tests.TestDoubles;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Shell.Main;
using NSubstitute;
using Xunit;

namespace Fluxo.Tests.ViewModels.Shell.Main;

public class UpcomingEventsPanelVMTests
{
    [Fact]
    public void UpcomingEventItemVM_FormatsMonthAndDay()
    {
        var item = new UpcomingEventItemVM(
            new DateOnly(2026, 6, 5),
            "Rent",
            "12,000",
            "Expense");

        Assert.Equal("JUN", item.MonthText);
        Assert.Equal("05", item.DayText);
        Assert.Equal("Rent", item.Title);
        Assert.Equal("12,000", item.AmountText);
        Assert.Equal("Expense", item.EventTypeText);
    }

    [Fact]
    public async Task LoadAsync_IncludesRecurringTransactionsDueWithinNext14Days()
    {
        var today = new DateTime(2026, 6, 14);
        var dueDate = today.AddDays(3);
        var vm = CreateVm(
            today,
            recurringTransactions:
            [
                new RecurringTransaction
                {
                    Id = 1,
                    Name = "Rent",
                    Amount = 12000m,
                    RecurringPeriod = RecurringPeriod.Monthly,
                    RecurringTime = dueDate.Day,
                    Type = RecurringTransactionType.Expense,
                    IsEnabled = true
                }
            ],
            savingGoals: []);

        await vm.LoadAsync();

        var item = Assert.Single(vm.Events);
        Assert.True(vm.HasEvents);
        Assert.Equal(DateOnly.FromDateTime(dueDate), item.Date);
        Assert.Equal("Rent", item.Title);
        Assert.Equal("12,000", item.AmountText);
        Assert.Equal("Expense", item.EventTypeText);
    }

    [Theory]
    [InlineData(RecurringTransactionType.Income, "Income")]
    [InlineData(RecurringTransactionType.Expense, "Expense")]
    [InlineData(RecurringTransactionType.GoalUpdate, "Goal")]
    public async Task LoadAsync_RecurringTransactionTypeText_UsesTransactionType(
        RecurringTransactionType transactionType,
        string expectedTypeText)
    {
        var today = new DateTime(2026, 6, 14);
        var vm = CreateVm(
            today,
            recurringTransactions:
            [
                new RecurringTransaction
                {
                    Id = 1,
                    Name = "Event",
                    Amount = 100m,
                    RecurringPeriod = RecurringPeriod.Monthly,
                    RecurringTime = today.Day,
                    Type = transactionType,
                    IsEnabled = true
                }
            ],
            savingGoals: []);

        await vm.LoadAsync();

        var item = Assert.Single(vm.Events);
        Assert.Equal(expectedTypeText, item.EventTypeText);
    }

    [Theory]
    [InlineData(AccountType.Credit)]
    [InlineData(AccountType.BNPL)]
    public async Task LoadAsync_CreditRecurringExpenseTypeText_UsesPayment(AccountType sourceType)
    {
        var today = new DateTime(2026, 6, 14);
        var vm = CreateVm(
            today,
            recurringTransactions:
            [
                new RecurringTransaction
                {
                    Id = 1,
                    Name = "Card payment",
                    Amount = 500m,
                    RecurringPeriod = RecurringPeriod.Monthly,
                    RecurringTime = today.Day,
                    Type = RecurringTransactionType.Expense,
                    Source = new Account { Id = 3, Name = "Visa", AccountType = sourceType },
                    IsEnabled = true
                }
            ],
            savingGoals: []);

        await vm.LoadAsync();

        var item = Assert.Single(vm.Events);
        Assert.Equal("Payment", item.EventTypeText);
    }

    [Fact]
    public async Task LoadAsync_ExcludesDisabledAndOutOfWindowRecurringTransactions()
    {
        var today = new DateTime(2026, 6, 14);
        var vm = CreateVm(
            today,
            recurringTransactions:
            [
                new RecurringTransaction
                {
                    Id = 1,
                    Name = "Disabled",
                    Amount = 100m,
                    RecurringPeriod = RecurringPeriod.Monthly,
                    RecurringTime = today.Day,
                    Type = RecurringTransactionType.Expense,
                    IsEnabled = false
                },
                new RecurringTransaction
                {
                    Id = 2,
                    Name = "Outside",
                    Amount = 200m,
                    RecurringPeriod = RecurringPeriod.Monthly,
                    RecurringTime = today.AddDays(-1).Day,
                    Type = RecurringTransactionType.Expense,
                    IsEnabled = true
                }
            ],
            savingGoals: []);

        await vm.LoadAsync();

        Assert.Empty(vm.Events);
        Assert.False(vm.HasEvents);
    }

    [Fact]
    public async Task LoadAsync_IncludesGoalDeadlineWithAmountLeftText()
    {
        var today = new DateTime(2026, 6, 14);
        var deadline = today.AddDays(10);
        var vm = CreateVm(
            today,
            recurringTransactions: [],
            savingGoals:
            [
                new SavingGoal
                {
                    Id = 1,
                    Name = "Emergency Fund",
                    CurrentAmount = 25000m,
                    TargetAmount = 50000m,
                    SavingEndDate = deadline
                }
            ]);

        await vm.LoadAsync();

        var item = Assert.Single(vm.Events);
        Assert.Equal(DateOnly.FromDateTime(deadline), item.Date);
        Assert.Equal("Emergency Fund deadline", item.Title);
        Assert.Equal("25,000 left from 50,000", item.AmountText);
        Assert.Equal("Goal Deadline", item.EventTypeText);
    }

    [Fact]
    public async Task LoadAsync_WhenGoalHasRecurringAndDeadlineInWindow_ShowsBothRows()
    {
        var today = new DateTime(2026, 6, 14);
        var recurringDate = today.AddDays(4);
        var deadline = today.AddDays(8);
        var goal = new SavingGoal
        {
            Id = 7,
            Name = "Laptop",
            CurrentAmount = 400m,
            TargetAmount = 1000m,
            SavingEndDate = deadline
        };
        var vm = CreateVm(
            today,
            recurringTransactions:
            [
                new RecurringTransaction
                {
                    Id = 10,
                    Name = "Laptop contribution",
                    Amount = 100m,
                    RecurringPeriod = RecurringPeriod.Monthly,
                    RecurringTime = recurringDate.Day,
                    Type = RecurringTransactionType.GoalUpdate,
                    GoalId = goal.Id,
                    Goal = goal,
                    IsEnabled = true
                }
            ],
            savingGoals: [goal]);

        await vm.LoadAsync();

        Assert.Equal(2, vm.Events.Count);
        Assert.Contains(vm.Events, item =>
            item.Title == "Laptop contribution" &&
            item.AmountText == "100" &&
            item.EventTypeText == "Goal");
        Assert.Contains(vm.Events, item =>
            item.Title == "Laptop deadline" &&
            item.AmountText == "600 left from 1,000" &&
            item.EventTypeText == "Goal Deadline");
    }

    [Fact]
    public async Task LoadAsync_OrdersEventsByDateThenTitle()
    {
        var today = new DateTime(2026, 6, 14);
        var sameDay = today.AddDays(2);
        var vm = CreateVm(
            today,
            recurringTransactions:
            [
                new RecurringTransaction
                {
                    Id = 1,
                    Name = "Zoo",
                    Amount = 1m,
                    RecurringPeriod = RecurringPeriod.Monthly,
                    RecurringTime = sameDay.Day,
                    Type = RecurringTransactionType.Expense,
                    IsEnabled = true
                },
                new RecurringTransaction
                {
                    Id = 2,
                    Name = "Alpha",
                    Amount = 2m,
                    RecurringPeriod = RecurringPeriod.Monthly,
                    RecurringTime = sameDay.Day,
                    Type = RecurringTransactionType.Expense,
                    IsEnabled = true
                }
            ],
            savingGoals:
            [
                new SavingGoal
                {
                    Id = 1,
                    Name = "Before",
                    CurrentAmount = 0m,
                    TargetAmount = 10m,
                    SavingEndDate = today.AddDays(1)
                }
            ]);

        await vm.LoadAsync();

        Assert.Equal(["Before deadline", "Alpha", "Zoo"], vm.Events.Select(item => item.Title).ToArray());
    }

    private static UpcomingEventsPanelVM CreateVm(
        DateTime today,
        IReadOnlyList<RecurringTransaction> recurringTransactions,
        IReadOnlyList<SavingGoal> savingGoals)
    {
        var recurringRepository = Substitute.For<IRecurringTransactionRepository>();
        recurringRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(recurringTransactions));

        var savingGoalRepository = Substitute.For<ISavingGoalRepository>();
        savingGoalRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(savingGoals));

        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.RecurringTransactions.Returns(recurringRepository);
        unitOfWork.SavingGoals.Returns(savingGoalRepository);

        var mapper = Substitute.For<IMapper>();
        mapper.Map<IReadOnlyList<RecurringTransactionDto>>(Arg.Any<object>())
            .Returns(recurringTransactions.Select(transaction => new RecurringTransactionDto
            {
                Id = transaction.Id,
                Name = transaction.Name,
                Amount = transaction.Amount,
                RecurringPeriod = transaction.RecurringPeriod,
                RecurringTime = transaction.RecurringTime,
                Type = transaction.Type,
                GoalId = transaction.GoalId,
                IsEnabled = transaction.IsEnabled
            }).ToList());
        mapper.Map<IReadOnlyList<RecurringTransactionVM>>(Arg.Any<object>())
            .Returns(recurringTransactions.Select(transaction => new RecurringTransactionVM
            {
                Id = transaction.Id,
                Name = transaction.Name,
                Amount = transaction.Amount,
                RecurringPeriod = transaction.RecurringPeriod,
                RecurringTime = transaction.RecurringTime,
                Type = transaction.Type,
                Source = transaction.Source is null
                    ? new AccountVM()
                    : new AccountVM
                    {
                        Id = transaction.Source.Id,
                        Name = transaction.Source.Name,
                        AccountType = transaction.Source.AccountType
                    },
                Goal = transaction.Goal is null
                    ? null
                    : new SavingGoalVM
                    {
                        Id = transaction.Goal.Id,
                        Name = transaction.Goal.Name,
                        CurrentAmount = transaction.Goal.CurrentAmount,
                        TargetAmount = transaction.Goal.TargetAmount,
                        SavingEndDate = transaction.Goal.SavingEndDate
                    },
                IsEnabled = transaction.IsEnabled
            }).ToList());

        return new UpcomingEventsPanelVM(
            new InlineDataOperationRunner(unitOfWork),
            mapper,
            () => today,
            new WeakReferenceMessenger());
    }
}
