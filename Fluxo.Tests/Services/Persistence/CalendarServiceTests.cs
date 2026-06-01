using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Services.Persistence;
using Fluxo.Tests.TestDoubles;
using NSubstitute;
using Xunit;

namespace Fluxo.Tests.Services.Persistence;

public sealed class CalendarServiceTests
{
    [Fact]
    public async Task GetCalendarDayAsync_FiltersSelectedDateAndBuildsSummaries()
    {
        var (sut, unitOfWork) = CreateSut();
        var selected = new DateOnly(2026, 6, 12);

        unitOfWork.ExpenseLogs.GetAllAsync(Arg.Any<CancellationToken>()).Returns([
            new ExpenseLog
            {
                Id = 1,
                Amount = 74m,
                DeductedOn = new DateTime(2026, 6, 12, 9, 0, 0),
                IsForDeletion = false,
                Expense = new Expense { Name = "Groceries", ExpenseTag = new ExpenseTag { Name = "Food", HexCode = "#00FF00" } },
                SpendingSource = new SpendingSource { Name = "Checking" }
            },
            new ExpenseLog
            {
                Id = 2,
                Amount = 10m,
                DeductedOn = new DateTime(2026, 6, 12, 10, 0, 0),
                IsForDeletion = true,
                Expense = new Expense { Name = "Deleted" },
                SpendingSource = new SpendingSource { Name = "Checking" }
            },
            new ExpenseLog
            {
                Id = 3,
                Amount = 99m,
                DeductedOn = new DateTime(2026, 6, 11, 23, 59, 0),
                IsForDeletion = false,
                Expense = new Expense { Name = "Different day" },
                SpendingSource = new SpendingSource { Name = "Checking" }
            }
        ]);

        unitOfWork.IncomeLogs.GetAllAsync(Arg.Any<CancellationToken>()).Returns([
            new IncomeLog
            {
                Id = 4,
                Name = "Freelance",
                Amount = 450m,
                AddedOn = new DateTime(2026, 6, 12, 14, 0, 0),
                SpendingSource = new SpendingSource { Name = "Checking" }
            },
            new IncomeLog
            {
                Id = 5,
                Name = "Other",
                Amount = 200m,
                AddedOn = new DateTime(2026, 6, 13, 1, 0, 0),
                SpendingSource = new SpendingSource { Name = "Checking" }
            }
        ]);

        unitOfWork.SavingGoals.GetAllAsync(Arg.Any<CancellationToken>()).Returns([
            new SavingGoal { Id = 6, Name = "Vacation", CurrentAmount = 100m, TargetAmount = 500m, SavingEndDate = new DateTime(2026, 6, 12) },
            new SavingGoal { Id = 7, Name = "No date", CurrentAmount = 0m, TargetAmount = 10m, SavingEndDate = null }
        ]);

        unitOfWork.RecurringTransactions.GetAllAsync(Arg.Any<CancellationToken>()).Returns([
            new RecurringTransaction
            {
                Id = 8,
                Name = "Rent",
                Amount = 1200m,
                IsEnabled = true,
                RecurringPeriod = RecurringPeriod.Monthly,
                RecurringTime = 12,
                Type = RecurringTransactionType.Expense,
                Source = new SpendingSource { Name = "Checking" }
            },
            new RecurringTransaction
            {
                Id = 9,
                Name = "Disabled",
                Amount = 1m,
                IsEnabled = false,
                RecurringPeriod = RecurringPeriod.Monthly,
                RecurringTime = 12,
                Type = RecurringTransactionType.Expense,
                Source = new SpendingSource { Name = "Checking" }
            }
        ]);

        var result = await sut.GetCalendarDayAsync(selected);

        Assert.Equal(selected, result.Date);
        Assert.Equal(74m, result.TotalSpent);
        Assert.Equal(450m, result.TotalEarned);
        Assert.Single(result.Expenses);
        Assert.Equal("Groceries", result.Expenses[0].Name);
        Assert.Single(result.Incomes);
        Assert.Equal("Freelance", result.Incomes[0].Name);
        Assert.Single(result.GoalDeadlines);
        Assert.Equal("Vacation", result.GoalDeadlines[0].Name);
        Assert.Single(result.RecurringTransactions);
        Assert.Equal("Rent", result.RecurringTransactions[0].Name);
        Assert.Equal(1, result.GoalsDue);
        Assert.Equal(1, result.PaymentsDue);
    }

    [Theory]
    [InlineData(RecurringPeriod.Weekly, 5, 2026, 6, 12, true)]
    [InlineData(RecurringPeriod.Biweekly, 5, 2026, 6, 12, true)]
    [InlineData(RecurringPeriod.Weekly, 4, 2026, 6, 12, false)]
    [InlineData(RecurringPeriod.Monthly, 12, 2026, 6, 12, true)]
    [InlineData(RecurringPeriod.Monthly, 11, 2026, 6, 12, false)]
    [InlineData(RecurringPeriod.None, 0, 2026, 6, 12, false)]
    public async Task GetCalendarDayAsync_FiltersRecurringTransactionsDueOnSelectedDate(
        RecurringPeriod period,
        int recurringTime,
        int year,
        int month,
        int day,
        bool expectedIncluded)
    {
        var (sut, unitOfWork) = CreateSut();
        unitOfWork.ExpenseLogs.GetAllAsync(Arg.Any<CancellationToken>()).Returns([]);
        unitOfWork.IncomeLogs.GetAllAsync(Arg.Any<CancellationToken>()).Returns([]);
        unitOfWork.SavingGoals.GetAllAsync(Arg.Any<CancellationToken>()).Returns([]);
        unitOfWork.RecurringTransactions.GetAllAsync(Arg.Any<CancellationToken>()).Returns([
            new RecurringTransaction
            {
                Id = 10,
                Name = "Candidate",
                Amount = 10m,
                IsEnabled = true,
                RecurringPeriod = period,
                RecurringTime = recurringTime,
                Type = RecurringTransactionType.Expense,
                Source = new SpendingSource { Name = "Checking" }
            }
        ]);

        var result = await sut.GetCalendarDayAsync(new DateOnly(year, month, day));

        Assert.Equal(expectedIncluded, result.RecurringTransactions.Count == 1);
    }

    private static (CalendarService Sut, IUnitOfWork UnitOfWork) CreateSut()
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.ExpenseLogs.Returns(Substitute.For<IExpenseLogRepository>());
        unitOfWork.IncomeLogs.Returns(Substitute.For<IIncomeLogRepository>());
        unitOfWork.SavingGoals.Returns(Substitute.For<ISavingGoalRepository>());
        unitOfWork.RecurringTransactions.Returns(Substitute.For<IRecurringTransactionRepository>());

        return (new CalendarService(new InlineDataOperationRunner(unitOfWork)), unitOfWork);
    }
}
