using System.Collections.ObjectModel;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Services.Notifications;
using Fluxo.Tests.TestDoubles;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Popups;
using Fluxo.ViewModels.Shell.Main;
using NSubstitute;
using Xunit;

namespace Fluxo.Tests.Services.Notifications;

public sealed class NotificationActionServiceTests
{
    [Fact]
    public async Task ExecuteChecklistActionAsync_Paid_ClearsOnlyMatchedNotifications_AndDoesNotMutateAccounts()
    {
        var persistedNotifications = new List<Notification>
        {
            new() { Id = 1, Type = "UpcomingPayment-10_20260501", Message = "Card 10 due", IsCleared = false },
            new() { Id = 2, Type = "UpcomingPayment-20_20260501", Message = "Card 20 due", IsCleared = false }
        };
        var spendingSources = new List<SpendingSource>
        {
            new()
            {
                Id = 1, Name = "Checking", SpendingSourceType = SpendingSourceType.Checking, Balance = 200m,
                SpentAmount = 0m
            },
            new()
            {
                Id = 10, Name = "Card 10", SpendingSourceType = SpendingSourceType.Credit, SpentAmount = 75m,
                Balance = 0m, DeductSource = 1
            }
        };
        var card = BuildChecklistCard(
            NotificationGroupCategory.UpcomingPayment,
            "UpcomingPayment-10_20260501",
            "UpcomingPayment-20_20260501");
        var expenseLogs = new List<ExpenseLog>();
        var incomeLogs = new List<IncomeLog>();

        var sut = CreateSut(
            persistedNotifications,
            spendingSources: spendingSources,
            expenseLogs: expenseLogs,
            incomeLogs: incomeLogs,
            expenseTags: [new ExpenseTag { Id = 1, Name = "Transfer", HexCode = "#000000" }]);

        var succeeded = await sut.ExecuteChecklistActionAsync(
            card,
            [new NotificationChecklistActionDecision(10, NotificationChecklistItemActionType.Paid, null)]);

        Assert.True(succeeded);
        Assert.True(persistedNotifications.Single(notification => notification.Id == 1).IsCleared);
        Assert.False(persistedNotifications.Single(notification => notification.Id == 2).IsCleared);
        Assert.Equal(200m, spendingSources.Single(source => source.Id == 1).Balance);
        Assert.Equal(75m, spendingSources.Single(source => source.Id == 10).SpentAmount);
        Assert.Empty(expenseLogs);
        Assert.Empty(incomeLogs);
    }

    [Fact]
    public async Task ExecuteChecklistActionAsync_ProcessUpcomingPayment_UpdatesSourcesPersistsLogs_AndClearsNotifications()
    {
        var persistedNotifications = new List<Notification>
        {
            new() { Id = 1, Type = "UpcomingPayment-10_20260501", Message = "Card 10 due", IsCleared = false },
            new() { Id = 2, Type = "LowBalance-8", Message = "Low balance", IsCleared = false }
        };
        var spendingSources = new List<SpendingSource>
        {
            new()
            {
                Id = 1, Name = "Checking", SpendingSourceType = SpendingSourceType.Checking, Balance = 250m
            },
            new()
            {
                Id = 10, Name = "Card 10", SpendingSourceType = SpendingSourceType.Credit, SpentAmount = 60m,
                DeductSource = 1
            }
        };
        var expenses = new List<Expense>();
        var expenseLogs = new List<ExpenseLog>();
        var incomeLogs = new List<IncomeLog>();
        var card = BuildChecklistCard(NotificationGroupCategory.UpcomingPayment, "UpcomingPayment-10_20260501");

        var sut = CreateSut(
            persistedNotifications,
            spendingSources: spendingSources,
            expenses: expenses,
            expenseLogs: expenseLogs,
            incomeLogs: incomeLogs,
            expenseTags: [new ExpenseTag { Id = 9, Name = "Transfer", HexCode = "#000000" }]);

        var succeeded = await sut.ExecuteChecklistActionAsync(
            card,
            [new NotificationChecklistActionDecision(10, NotificationChecklistItemActionType.Process, null)]);

        Assert.True(succeeded);
        Assert.Equal(190m, spendingSources.Single(source => source.Id == 1).Balance);
        Assert.Equal(0m, spendingSources.Single(source => source.Id == 10).SpentAmount);
        Assert.Single(expenses);
        Assert.Single(expenseLogs);
        Assert.Single(incomeLogs);
        Assert.Equal(60m, expenseLogs[0].Amount);
        Assert.Equal(1, expenseLogs[0].SpendingSourceId);
        Assert.Equal(60m, incomeLogs[0].Amount);
        Assert.Equal(10, incomeLogs[0].SpendingSourceId);
        Assert.True(persistedNotifications.Single(notification => notification.Id == 1).IsCleared);
        Assert.False(persistedNotifications.Single(notification => notification.Id == 2).IsCleared);
    }

    [Fact]
    public async Task ExecuteChecklistActionAsync_ProcessRecurringExpense_UsesSelectedSource_AndClearsNotifications()
    {
        var persistedNotifications = new List<Notification>
        {
            new() { Id = 1, Type = "RecurringTransactionDue-77_20260501", Message = "Rent due", IsCleared = false }
        };
        var recurring = new List<RecurringTransaction> { new() { Id = 77, Name = "Rent", Amount = 25m, SourceId = 4, TagId = 5, Type = RecurringTransactionType.Expense, IsEnabled = true, RecurringDate = 10 } };
        var spendingSources = new List<SpendingSource>
        {
            new()
            {
                Id = 4, Name = "Default Card", SpendingSourceType = SpendingSourceType.Credit, SpentAmount = 10m
            },
            new()
            {
                Id = 8, Name = "Checking", SpendingSourceType = SpendingSourceType.Checking, Balance = 100m
            }
        };
        var expenseLogs = new List<ExpenseLog>();
        var card = BuildChecklistCard(NotificationGroupCategory.RecurringTransactionDue, "RecurringTransactionDue-77_20260501");

        var sut = CreateSut(
            persistedNotifications,
            spendingSources: spendingSources,
            recurringTransactions: recurring,
            expenseLogs: expenseLogs,
            expenseTags: [new ExpenseTag { Id = 5, Name = "Needs", HexCode = "#111111" }]);

        var succeeded = await sut.ExecuteChecklistActionAsync(
            card,
            [new NotificationChecklistActionDecision(77, NotificationChecklistItemActionType.Process, 8, 25m, 5)]);

        Assert.True(succeeded);
        Assert.Equal(75m, spendingSources.Single(source => source.Id == 8).Balance);
        Assert.Single(expenseLogs);
        Assert.Equal("Rent", expenseLogs[0].Expense.Name);
        Assert.Equal(8, expenseLogs[0].SpendingSourceId);
        Assert.Equal(25m, expenseLogs[0].Amount);
        Assert.True(persistedNotifications[0].IsCleared);
    }

    [Fact]
    public async Task ExecuteChecklistActionAsync_ReturnsFalse_ForNonActionableCategory()
    {
        var persistedNotifications = new List<Notification>
        {
            new() { Id = 1, Type = "LowBalance-99", Message = "Low balance", IsCleared = false }
        };

        var card = BuildChecklistCard(NotificationGroupCategory.LowBalance, "LowBalance-99");
        var sut = CreateSut(persistedNotifications);

        var succeeded = await sut.ExecuteChecklistActionAsync(
            card,
            [new NotificationChecklistActionDecision(99, NotificationChecklistItemActionType.Process, null)]);

        Assert.False(succeeded);
        Assert.False(persistedNotifications[0].IsCleared);
    }

    [Fact]
    public async Task ExecuteChecklistActionAsync_MixedDecisions_OnlyMutatesPaidAndProcessRows()
    {
        var persistedNotifications = new List<Notification>
        {
            new() { Id = 1, Type = "UpcomingPayment-10_20260501", Message = "Card 10 due", IsCleared = false },
            new() { Id = 2, Type = "UpcomingPayment-20_20260501", Message = "Card 20 due", IsCleared = false },
            new() { Id = 3, Type = "UpcomingPayment-30_20260501", Message = "Card 30 due", IsCleared = false }
        };
        var spendingSources = new List<SpendingSource>
        {
            new()
            {
                Id = 1, Name = "Checking", SpendingSourceType = SpendingSourceType.Checking, Balance = 400m
            },
            new()
            {
                Id = 10, Name = "Card 10", SpendingSourceType = SpendingSourceType.Credit, SpentAmount = 30m,
                DeductSource = 1
            },
            new()
            {
                Id = 20, Name = "Card 20", SpendingSourceType = SpendingSourceType.Credit, SpentAmount = 50m,
                DeductSource = 1
            },
            new()
            {
                Id = 30, Name = "Card 30", SpendingSourceType = SpendingSourceType.Credit, SpentAmount = 60m,
                DeductSource = 1
            }
        };
        var expenses = new List<Expense>();
        var expenseLogs = new List<ExpenseLog>();
        var incomeLogs = new List<IncomeLog>();
        var card = BuildChecklistCard(
            NotificationGroupCategory.UpcomingPayment,
            "UpcomingPayment-10_20260501",
            "UpcomingPayment-20_20260501",
            "UpcomingPayment-30_20260501");

        var sut = CreateSut(
            persistedNotifications,
            spendingSources: spendingSources,
            expenses: expenses,
            expenseLogs: expenseLogs,
            incomeLogs: incomeLogs,
            expenseTags: [new ExpenseTag { Id = 11, Name = "Transfer", HexCode = "#555555" }]);

        var succeeded = await sut.ExecuteChecklistActionAsync(
            card,
            [
                new NotificationChecklistActionDecision(10, NotificationChecklistItemActionType.Paid, null),
                new NotificationChecklistActionDecision(20, NotificationChecklistItemActionType.Process, null),
                new NotificationChecklistActionDecision(30, NotificationChecklistItemActionType.Ignore, null)
            ]);

        Assert.True(succeeded);
        Assert.True(persistedNotifications.Single(notification => notification.Id == 1).IsCleared);
        Assert.True(persistedNotifications.Single(notification => notification.Id == 2).IsCleared);
        Assert.False(persistedNotifications.Single(notification => notification.Id == 3).IsCleared);
        Assert.Equal(350m, spendingSources.Single(source => source.Id == 1).Balance);
        Assert.Equal(30m, spendingSources.Single(source => source.Id == 10).SpentAmount);
        Assert.Equal(0m, spendingSources.Single(source => source.Id == 20).SpentAmount);
        Assert.Equal(60m, spendingSources.Single(source => source.Id == 30).SpentAmount);
        Assert.Single(expenses);
        Assert.Single(expenseLogs);
        Assert.Single(incomeLogs);
        Assert.Equal(50m, expenseLogs[0].Amount);
        Assert.Equal(50m, incomeLogs[0].Amount);
    }

    [Fact]
    public async Task ExecuteGoalActionAsync_MarkAsReached_SetsCurrentAmountToTarget_AndClearsNotifications()
    {
        var goal = new SavingGoal { Id = 7, TargetAmount = 500m, CurrentAmount = 125m };
        var persistedGoals = new List<SavingGoal> { goal };
        var persistedNotifications = new List<Notification>
        {
            new() { Id = 1, Type = "GoalDeadline-7_20260501", Message = "Goal 7 deadline", IsCleared = false },
            new() { Id = 2, Type = "GoalDeadline-8_20260501", Message = "Goal 8 deadline", IsCleared = false }
        };

        var card = BuildChecklistCard(NotificationGroupCategory.GoalDeadline, "GoalDeadline-7_20260501");
        var sut = CreateSut(persistedNotifications, persistedGoals: persistedGoals);

        var succeeded = await sut.ExecuteGoalActionAsync(card, GoalDeadlineActionType.MarkAsReached);

        Assert.True(succeeded);
        Assert.Equal(goal.TargetAmount, goal.CurrentAmount);
        Assert.True(persistedNotifications.Single(n => n.Id == 1).IsCleared);
        Assert.False(persistedNotifications.Single(n => n.Id == 2).IsCleared);
    }

    [Fact]
    public async Task ExecuteGoalActionAsync_AbandonGoal_RemovesGoal_AndClearsNotifications()
    {
        var goal = new SavingGoal { Id = 11, TargetAmount = 1000m, CurrentAmount = 400m };
        var persistedGoals = new List<SavingGoal> { goal };
        var persistedNotifications = new List<Notification>
        {
            new() { Id = 1, Type = "GoalDeadline-11_20260501", Message = "Goal 11 deadline", IsCleared = false }
        };

        var card = BuildChecklistCard(NotificationGroupCategory.GoalDeadline, "GoalDeadline-11_20260501");
        var sut = CreateSut(persistedNotifications, persistedGoals: persistedGoals);

        var succeeded = await sut.ExecuteGoalActionAsync(card, GoalDeadlineActionType.AbandonGoal);

        Assert.True(succeeded);
        Assert.Empty(persistedGoals);
        Assert.True(persistedNotifications[0].IsCleared);
    }

    [Fact]
    public async Task ExecuteGoalActionAsync_DoesNotClearNotifications_WhenMutationFails()
    {
        var persistedNotifications = new List<Notification>
        {
            new() { Id = 1, Type = "GoalDeadline-99_20260501", Message = "Missing goal", IsCleared = false }
        };
        var card = BuildChecklistCard(NotificationGroupCategory.GoalDeadline, "GoalDeadline-99_20260501");
        var sut = CreateSut(persistedNotifications);

        var succeeded = await sut.ExecuteGoalActionAsync(card, GoalDeadlineActionType.MarkAsReached);

        Assert.False(succeeded);
        Assert.False(persistedNotifications[0].IsCleared);
    }

    private static NotificationItemVM BuildChecklistCard(NotificationGroupCategory category, params string[] types)
    {
        return new NotificationItemVM
        {
            Category = category,
            Notifications = new ObservableCollection<NotificationVM>(
                types.Select(type => new NotificationVM { Type = type, Message = type }))
        };
    }

    private static NotificationActionService CreateSut(
        List<Notification> persistedNotifications,
        List<SavingGoal>? persistedGoals = null,
        List<SpendingSource>? spendingSources = null,
        List<Expense>? expenses = null,
        List<RecurringTransaction>? recurringTransactions = null,
        List<ExpenseLog>? expenseLogs = null,
        List<IncomeLog>? incomeLogs = null,
        List<ExpenseTag>? expenseTags = null)
    {
        persistedGoals ??= [];
        spendingSources ??= [];
        expenses ??= [];
        recurringTransactions ??= [];
        expenseLogs ??= [];
        incomeLogs ??= [];
        expenseTags ??= [];

        var notificationRepository = Substitute.For<INotificationRepository>();
        notificationRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<Notification>>(persistedNotifications.ToList()));
        notificationRepository.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(0));
        notificationRepository
            .When(repository => repository.Update(Arg.Any<Notification>()))
            .Do(call =>
            {
                var updated = call.Arg<Notification>();
                var existing = persistedNotifications.FirstOrDefault(item => item.Id == updated.Id);
                if (existing is null)
                {
                    persistedNotifications.Add(updated);
                    return;
                }

                existing.Type = updated.Type;
                existing.Header = updated.Header;
                existing.Message = updated.Message;
                existing.CreatedOn = updated.CreatedOn;
                existing.IsCleared = updated.IsCleared;
                existing.IsForDeletion = updated.IsForDeletion;
            });

        var savingGoalRepository = Substitute.For<ISavingGoalRepository>();
        savingGoalRepository.GetByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var id = call.Arg<int>();
                return Task.FromResult<SavingGoal?>(persistedGoals.FirstOrDefault(goal => goal.Id == id));
            });
        savingGoalRepository
            .When(repository => repository.Update(Arg.Any<SavingGoal>()))
            .Do(call =>
            {
                var updated = call.Arg<SavingGoal>();
                var existing = persistedGoals.FirstOrDefault(goal => goal.Id == updated.Id);
                if (existing is null)
                    return;

                existing.Name = updated.Name;
                existing.TargetAmount = updated.TargetAmount;
                existing.CurrentAmount = updated.CurrentAmount;
                existing.SavingEndDate = updated.SavingEndDate;
                existing.CreatedOn = updated.CreatedOn;
            });
        savingGoalRepository
            .When(repository => repository.Remove(Arg.Any<SavingGoal>()))
            .Do(call =>
            {
                var removed = call.Arg<SavingGoal>();
                persistedGoals.RemoveAll(goal => goal.Id == removed.Id);
            });

        var spendingSourceRepository = Substitute.For<ISpendingSourceRepository>();
        spendingSourceRepository.GetByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var id = call.Arg<int>();
                return Task.FromResult<SpendingSource?>(spendingSources.FirstOrDefault(source => source.Id == id));
            });
        spendingSourceRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<SpendingSource>>(spendingSources.ToList()));
        spendingSourceRepository
            .When(repository => repository.Update(Arg.Any<SpendingSource>()))
            .Do(call =>
            {
                var updated = call.Arg<SpendingSource>();
                var existing = spendingSources.FirstOrDefault(source => source.Id == updated.Id);
                if (existing is null)
                {
                    spendingSources.Add(updated);
                    return;
                }

                existing.Name = updated.Name;
                existing.SpendingSourceType = updated.SpendingSourceType;
                existing.AccountLimit = updated.AccountLimit;
                existing.SpentAmount = updated.SpentAmount;
                existing.Balance = updated.Balance;
                existing.MonthlyDueDate = updated.MonthlyDueDate;
                existing.DeductSource = updated.DeductSource;
                existing.InterestRate = updated.InterestRate;
                existing.ShowOnUI = updated.ShowOnUI;
                existing.IsEnabled = updated.IsEnabled;
                existing.IsForDeletion = updated.IsForDeletion;
            });

        var expenseRepository = Substitute.For<IExpenseRepository>();
        expenseRepository.GetByExpenseIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var id = call.Arg<int>();
                return Task.FromResult<Expense?>(expenses.FirstOrDefault(expense => expense.Id == id));
            });
        expenseRepository.GetByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var id = call.Arg<int>();
                return Task.FromResult<Expense?>(expenses.FirstOrDefault(expense => expense.Id == id));
            });
        expenseRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<Expense>>(expenses.ToList()));
        expenseRepository.AddAsync(Arg.Any<Expense>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var expense = call.Arg<Expense>();
                if (expense.Id <= 0)
                    expense.Id = expenses.Count == 0 ? 1 : expenses.Max(item => item.Id) + 1;

                expenses.Add(expense);
                return Task.CompletedTask;
            });

        var expenseLogRepository = Substitute.For<IExpenseLogRepository>();
        expenseLogRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<ExpenseLog>>(expenseLogs.ToList()));
        expenseLogRepository.AddAsync(Arg.Any<ExpenseLog>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var expenseLog = call.Arg<ExpenseLog>();
                if (expenseLog.Id <= 0)
                    expenseLog.Id = expenseLogs.Count == 0 ? 1 : expenseLogs.Max(item => item.Id) + 1;

                if (expenseLog.ExpenseId <= 0 && expenseLog.Expense is not null)
                    expenseLog.ExpenseId = expenseLog.Expense.Id;

                expenseLogs.Add(expenseLog);
                return Task.CompletedTask;
            });

        var incomeLogRepository = Substitute.For<IIncomeLogRepository>();
        incomeLogRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<IncomeLog>>(incomeLogs.ToList()));
        incomeLogRepository.AddAsync(Arg.Any<IncomeLog>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var incomeLog = call.Arg<IncomeLog>();
                if (incomeLog.Id <= 0)
                    incomeLog.Id = incomeLogs.Count == 0 ? 1 : incomeLogs.Max(item => item.Id) + 1;

                incomeLogs.Add(incomeLog);
                return Task.CompletedTask;
            });

        var expenseTagRepository = Substitute.For<IExpenseTagRepository>();
        expenseTagRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<ExpenseTag>>(expenseTags.ToList()));
        expenseTagRepository.GetByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var id = call.Arg<int>();
                return Task.FromResult<ExpenseTag?>(expenseTags.FirstOrDefault(tag => tag.Id == id));
            });

        var recurringRepository = Substitute.For<IRecurringTransactionRepository>();
        recurringRepository.GetByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var id = call.Arg<int>();
                return Task.FromResult<RecurringTransaction?>(recurringTransactions.FirstOrDefault(item => item.Id == id));
            });
        recurringRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<RecurringTransaction>>(recurringTransactions.ToList()));
        recurringRepository
            .When(repository => repository.Update(Arg.Any<RecurringTransaction>()))
            .Do(call =>
            {
                var updated = call.Arg<RecurringTransaction>();
                var existing = recurringTransactions.FirstOrDefault(item => item.Id == updated.Id);
                if (existing is null)
                {
                    recurringTransactions.Add(updated);
                    return;
                }

                existing.Amount = updated.Amount;
                existing.SourceId = updated.SourceId;
                existing.TagId = updated.TagId;
                existing.GoalId = updated.GoalId;
            });

        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.Notifications.Returns(notificationRepository);
        unitOfWork.SavingGoals.Returns(savingGoalRepository);
        unitOfWork.SpendingSources.Returns(spendingSourceRepository);
        unitOfWork.Expenses.Returns(expenseRepository);
        unitOfWork.ExpenseLogs.Returns(expenseLogRepository);
        unitOfWork.IncomeLogs.Returns(incomeLogRepository);
        unitOfWork.ExpenseTags.Returns(expenseTagRepository);
        unitOfWork.RecurringTransactions.Returns(recurringRepository);
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(1));

        return new NotificationActionService(new InlineDataOperationRunner(unitOfWork));
    }
}
