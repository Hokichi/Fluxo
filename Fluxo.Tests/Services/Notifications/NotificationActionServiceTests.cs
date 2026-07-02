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
        var accounts = new List<Account>
        {
            new()
            {
                Id = 1, Name = "Checking", AccountType = AccountType.Checking, Balance = 200m,
                SpentAmount = 0m
            },
            new()
            {
                Id = 10, Name = "Card 10", AccountType = AccountType.Credit, SpentAmount = 75m,
                Balance = 0m, DeductSource = 1
            }
        };
        var card = BuildChecklistCard(
            NotificationGroupCategory.UpcomingPayment,
            "UpcomingPayment-10_20260501",
            "UpcomingPayment-20_20260501");
        var transactions = new List<Transaction>();

        var sut = CreateSut(
            persistedNotifications,
            accounts: accounts,
            transactions: transactions,
            tags: [new Tag { Id = 1, Name = "Transfer", HexCode = "#000000" }]);

        var succeeded = await sut.ExecuteChecklistActionAsync(
            card,
            [new NotificationChecklistActionDecision(10, NotificationChecklistItemActionType.Paid, null)]);

        Assert.True(succeeded);
        Assert.True(persistedNotifications.Single(notification => notification.Id == 1).IsCleared);
        Assert.False(persistedNotifications.Single(notification => notification.Id == 2).IsCleared);
        Assert.Equal(200m, accounts.Single(source => source.Id == 1).Balance);
        Assert.Equal(75m, accounts.Single(source => source.Id == 10).SpentAmount);
        Assert.Empty(transactions);
    }

    [Fact]
    public async Task ExecuteChecklistActionAsync_ProcessUpcomingPayment_UpdatesSourcesPersistsLogs_AndClearsNotifications()
    {
        var persistedNotifications = new List<Notification>
        {
            new() { Id = 1, Type = "UpcomingPayment-10_20260501", Message = "Card 10 due", IsCleared = false },
            new() { Id = 2, Type = "LowBalance-8", Message = "Low balance", IsCleared = false }
        };
        var accounts = new List<Account>
        {
            new()
            {
                Id = 1, Name = "Checking", AccountType = AccountType.Checking, Balance = 250m
            },
            new()
            {
                Id = 10, Name = "Card 10", AccountType = AccountType.Credit, SpentAmount = 60m,
                DeductSource = 1
            }
        };
        var transactions = new List<Transaction>();
        var card = BuildChecklistCard(NotificationGroupCategory.UpcomingPayment, "UpcomingPayment-10_20260501");

        var sut = CreateSut(
            persistedNotifications,
            accounts: accounts,
            transactions: transactions,
            tags: [new Tag { Id = 9, Name = "Transfer", HexCode = "#000000" }]);

        var succeeded = await sut.ExecuteChecklistActionAsync(
            card,
            [new NotificationChecklistActionDecision(10, NotificationChecklistItemActionType.Process, null)]);

        Assert.True(succeeded);
        Assert.Equal(190m, accounts.Single(source => source.Id == 1).Balance);
        Assert.Equal(0m, accounts.Single(source => source.Id == 10).SpentAmount);
        Assert.Equal(2, transactions.Count);
        var expense = Assert.Single(transactions, transaction => transaction.Type == TransactionType.Expense);
        var income = Assert.Single(transactions, transaction => transaction.Type == TransactionType.Income);
        Assert.Equal(60m, expense.Amount);
        Assert.Equal(1, expense.SourceAccountId);
        Assert.Equal(60m, income.Amount);
        Assert.Equal(10, income.SourceAccountId);
        Assert.True(persistedNotifications.Single(notification => notification.Id == 1).IsCleared);
        Assert.False(persistedNotifications.Single(notification => notification.Id == 2).IsCleared);
    }

    [Fact]
    public async Task ExecuteChecklistActionAsync_ProcessLatePayment_UsesSelectedAmountAndCheckingSource()
    {
        var persistedNotifications = new List<Notification>
        {
            new() { Id = 1, Type = "LatePayment-10_20260501", Message = "Card 10 late", IsCleared = false }
        };
        var accounts = new List<Account>
        {
            new() { Id = 1, Name = "Old Checking", AccountType = AccountType.Checking, Balance = 500m },
            new() { Id = 4, Name = "Selected Checking", AccountType = AccountType.Checking, Balance = 300m },
            new()
            {
                Id = 10,
                Name = "Card 10",
                AccountType = AccountType.Credit,
                SpentAmount = 100m,
                DeductSource = 1
            }
        };
        var transactions = new List<Transaction>();
        var sut = CreateSut(
            persistedNotifications,
            accounts: accounts,
            transactions: transactions,
            tags: [new Tag { Id = 9, Name = "Balance Update", HexCode = "#000000" }]);

        var succeeded = await sut.ExecuteChecklistActionAsync(
            BuildChecklistCard(NotificationGroupCategory.LatePayment, "LatePayment-10_20260501"),
            [new NotificationChecklistActionDecision(10, NotificationChecklistItemActionType.Process, 4, 60m)]);

        Assert.True(succeeded);
        Assert.Equal(500m, accounts.Single(account => account.Id == 1).Balance);
        Assert.Equal(240m, accounts.Single(account => account.Id == 4).Balance);
        Assert.Equal(40m, accounts.Single(account => account.Id == 10).SpentAmount);
        var expense = Assert.Single(transactions, transaction => transaction.Type == TransactionType.Expense);
        Assert.Equal(60m, expense.Amount);
        Assert.Equal(4, expense.SourceAccountId);
        Assert.Equal(9, expense.TagId);
        Assert.True(expense.IsExcludedFromBudget);
    }

    [Fact]
    public async Task ExecuteChecklistActionAsync_ProcessRecurringExpense_UsesSelectedSource_AndClearsNotifications()
    {
        var persistedNotifications = new List<Notification>
        {
            new() { Id = 1, Type = "RecurringTransactionDue-77_20260501", Message = "Rent due", IsCleared = false }
        };
        var recurring = new List<RecurringTransaction> { new() { Id = 77, Name = "Rent", Amount = 25m, SourceId = 4, TagId = 5, Type = RecurringTransactionType.Expense, IsEnabled = true, RecurringPeriod = RecurringPeriod.Monthly, RecurringTime = 10 } };
        var accounts = new List<Account>
        {
            new()
            {
                Id = 4, Name = "Default Card", AccountType = AccountType.Credit, SpentAmount = 10m
            },
            new()
            {
                Id = 8, Name = "Checking", AccountType = AccountType.Checking, Balance = 100m
            }
        };
        var transactions = new List<Transaction>();
        var card = BuildChecklistCard(NotificationGroupCategory.RecurringTransactionDue, "RecurringTransactionDue-77_20260501");

        var sut = CreateSut(
            persistedNotifications,
            accounts: accounts,
            recurringTransactions: recurring,
            transactions: transactions,
            tags: [new Tag { Id = 5, Name = "Needs", HexCode = "#111111" }]);

        var succeeded = await sut.ExecuteChecklistActionAsync(
            card,
            [new NotificationChecklistActionDecision(77, NotificationChecklistItemActionType.Process, 8, 25m, 5)]);

        Assert.True(succeeded);
        Assert.Equal(75m, accounts.Single(source => source.Id == 8).Balance);
        var expense = Assert.Single(transactions);
        Assert.Equal("Rent", expense.Name);
        Assert.Equal(8, expense.SourceAccountId);
        Assert.Equal(25m, expense.Amount);
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
        var accounts = new List<Account>
        {
            new()
            {
                Id = 1, Name = "Checking", AccountType = AccountType.Checking, Balance = 400m
            },
            new()
            {
                Id = 10, Name = "Card 10", AccountType = AccountType.Credit, SpentAmount = 30m,
                DeductSource = 1
            },
            new()
            {
                Id = 20, Name = "Card 20", AccountType = AccountType.Credit, SpentAmount = 50m,
                DeductSource = 1
            },
            new()
            {
                Id = 30, Name = "Card 30", AccountType = AccountType.Credit, SpentAmount = 60m,
                DeductSource = 1
            }
        };
        var transactions = new List<Transaction>();
        var card = BuildChecklistCard(
            NotificationGroupCategory.UpcomingPayment,
            "UpcomingPayment-10_20260501",
            "UpcomingPayment-20_20260501",
            "UpcomingPayment-30_20260501");

        var sut = CreateSut(
            persistedNotifications,
            accounts: accounts,
            transactions: transactions,
            tags: [new Tag { Id = 11, Name = "Transfer", HexCode = "#555555" }]);

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
        Assert.Equal(350m, accounts.Single(source => source.Id == 1).Balance);
        Assert.Equal(30m, accounts.Single(source => source.Id == 10).SpentAmount);
        Assert.Equal(0m, accounts.Single(source => source.Id == 20).SpentAmount);
        Assert.Equal(60m, accounts.Single(source => source.Id == 30).SpentAmount);
        Assert.Equal(2, transactions.Count);
        Assert.Equal(50m, Assert.Single(transactions, transaction => transaction.Type == TransactionType.Expense).Amount);
        Assert.Equal(50m, Assert.Single(transactions, transaction => transaction.Type == TransactionType.Income).Amount);
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
        List<Account>? accounts = null,
        List<RecurringTransaction>? recurringTransactions = null,
        List<Transaction>? transactions = null,
        List<Tag>? tags = null)
    {
        persistedGoals ??= [];
        accounts ??= [];
        recurringTransactions ??= [];
        transactions ??= [];
        tags ??= [];

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

        var accountRepository = Substitute.For<IAccountRepository>();
        accountRepository.GetByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var id = call.Arg<int>();
                return Task.FromResult<Account?>(accounts.FirstOrDefault(source => source.Id == id));
            });
        accountRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<Account>>(accounts.ToList()));
        accountRepository
            .When(repository => repository.Update(Arg.Any<Account>()))
            .Do(call =>
            {
                var updated = call.Arg<Account>();
                var existing = accounts.FirstOrDefault(source => source.Id == updated.Id);
                if (existing is null)
                {
                    accounts.Add(updated);
                    return;
                }

                existing.Name = updated.Name;
                existing.AccountType = updated.AccountType;
                existing.AccountLimit = updated.AccountLimit;
                existing.SpentAmount = updated.SpentAmount;
                existing.Balance = updated.Balance;
                existing.MonthlyDueDate = updated.MonthlyDueDate;
                existing.DeductSource = updated.DeductSource;
                existing.InterestRate = updated.InterestRate;
                existing.PinnedOnUI = updated.PinnedOnUI;
                existing.IsEnabled = updated.IsEnabled;
                existing.IsForDeletion = updated.IsForDeletion;
            });

        var transactionRepository = Substitute.For<ITransactionRepository>();
        transactionRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<Transaction>>(transactions.ToList()));
        transactionRepository.AddAsync(Arg.Any<Transaction>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var transaction = call.Arg<Transaction>();
                if (transaction.Id <= 0)
                    transaction.Id = transactions.Count == 0 ? 1 : transactions.Max(item => item.Id) + 1;

                transactions.Add(transaction);
                return Task.CompletedTask;
            });

        var tagRepository = Substitute.For<ITagRepository>();
        tagRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<Tag>>(tags.ToList()));
        tagRepository.GetByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var id = call.Arg<int>();
                return Task.FromResult<Tag?>(tags.FirstOrDefault(tag => tag.Id == id));
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
        unitOfWork.Accounts.Returns(accountRepository);
        unitOfWork.Transactions.Returns(transactionRepository);
        unitOfWork.Tags.Returns(tagRepository);
        unitOfWork.RecurringTransactions.Returns(recurringRepository);
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(1));

        return new NotificationActionService(new InlineDataOperationRunner(unitOfWork));
    }
}
