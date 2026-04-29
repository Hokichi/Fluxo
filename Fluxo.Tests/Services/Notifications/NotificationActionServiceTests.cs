using System.Collections.ObjectModel;
using Fluxo.Core.Entities;
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
    public async Task ExecuteChecklistActionAsync_ClearsOnlySelectedMappedNotifications()
    {
        var persistedNotifications = new List<Notification>
        {
            new() { Id = 1, Type = "UpcomingPayment-10_20260501", Message = "Card 10 due", IsCleared = false },
            new() { Id = 2, Type = "UpcomingPayment-20_20260501", Message = "Card 20 due", IsCleared = false },
            new() { Id = 3, Type = "LowBalance-99", Message = "Low balance", IsCleared = false }
        };

        var card = new NotificationItemVM
        {
            Category = NotificationGroupCategory.UpcomingPayment,
            Notifications = new ObservableCollection<NotificationVM>
            {
                new() { Type = "UpcomingPayment-10_20260501", Message = "Card 10 due" },
                new() { Type = "UpcomingPayment-20_20260501", Message = "Card 20 due" }
            }
        };

        var sut = CreateSut(persistedNotifications, out _, out _);

        var succeeded = await sut.ExecuteChecklistActionAsync(
            card,
            [new NotificationChecklistActionDecision(10, NotificationChecklistItemActionType.Process, null)]);

        Assert.True(succeeded);
        Assert.True(persistedNotifications.Single(n => n.Id == 1).IsCleared);
        Assert.False(persistedNotifications.Single(n => n.Id == 2).IsCleared);
        Assert.False(persistedNotifications.Single(n => n.Id == 3).IsCleared);
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

        var card = new NotificationItemVM
        {
            Category = NotificationGroupCategory.GoalDeadline,
            Notifications = new ObservableCollection<NotificationVM>
            {
                new() { Type = "GoalDeadline-7_20260501", Message = "Goal 7 deadline" }
            }
        };

        var sut = CreateSut(persistedNotifications, persistedGoals, out _, out _);

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

        var card = new NotificationItemVM
        {
            Category = NotificationGroupCategory.GoalDeadline,
            Notifications = new ObservableCollection<NotificationVM>
            {
                new() { Type = "GoalDeadline-11_20260501", Message = "Goal 11 deadline" }
            }
        };

        var sut = CreateSut(persistedNotifications, persistedGoals, out _, out _);

        var succeeded = await sut.ExecuteGoalActionAsync(card, GoalDeadlineActionType.AbandonGoal);

        Assert.True(succeeded);
        Assert.Empty(persistedGoals);
        Assert.True(persistedNotifications[0].IsCleared);
    }

    [Fact]
    public async Task ExecuteGoalActionAsync_DoesNotClearNotifications_WhenMutationFails()
    {
        var persistedGoals = new List<SavingGoal>();
        var persistedNotifications = new List<Notification>
        {
            new() { Id = 1, Type = "GoalDeadline-99_20260501", Message = "Missing goal", IsCleared = false }
        };

        var card = new NotificationItemVM
        {
            Category = NotificationGroupCategory.GoalDeadline,
            Notifications = new ObservableCollection<NotificationVM>
            {
                new() { Type = "GoalDeadline-99_20260501", Message = "Missing goal" }
            }
        };

        var sut = CreateSut(persistedNotifications, persistedGoals, out _, out _);

        var succeeded = await sut.ExecuteGoalActionAsync(card, GoalDeadlineActionType.MarkAsReached);

        Assert.False(succeeded);
        Assert.False(persistedNotifications[0].IsCleared);
    }

    [Fact]
    public async Task ExecuteChecklistActionAsync_ReturnsFalse_ForNonActionableCategory()
    {
        var persistedNotifications = new List<Notification>
        {
            new() { Id = 1, Type = "LowBalance-99", Message = "Low balance", IsCleared = false }
        };

        var card = new NotificationItemVM
        {
            Category = NotificationGroupCategory.LowBalance,
            Notifications = new ObservableCollection<NotificationVM>
            {
                new() { Type = "LowBalance-99", Message = "Low balance" }
            }
        };

        var sut = CreateSut(persistedNotifications, out _, out _);

        var succeeded = await sut.ExecuteChecklistActionAsync(
            card,
            [new NotificationChecklistActionDecision(99, NotificationChecklistItemActionType.Process, null)]);

        Assert.False(succeeded);
        Assert.False(persistedNotifications[0].IsCleared);
    }

    private static NotificationActionService CreateSut(
        List<Notification> persistedNotifications,
        out INotificationRepository notificationRepository,
        out ISavingGoalRepository savingGoalRepository)
    {
        return CreateSut(persistedNotifications, [], out notificationRepository, out savingGoalRepository);
    }

    private static NotificationActionService CreateSut(
        List<Notification> persistedNotifications,
        List<SavingGoal> persistedGoals,
        out INotificationRepository notificationRepository,
        out ISavingGoalRepository savingGoalRepository)
    {
        notificationRepository = Substitute.For<INotificationRepository>();
        notificationRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<Notification>>(persistedNotifications.ToList()));
        notificationRepository.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(0));
        notificationRepository
            .When(repo => repo.Update(Arg.Any<Notification>()))
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

        savingGoalRepository = Substitute.For<ISavingGoalRepository>();
        savingGoalRepository.GetByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var id = call.Arg<int>();
                return Task.FromResult<SavingGoal?>(persistedGoals.FirstOrDefault(goal => goal.Id == id));
            });
        savingGoalRepository
            .When(repo => repo.Update(Arg.Any<SavingGoal>()))
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
            .When(repo => repo.Remove(Arg.Any<SavingGoal>()))
            .Do(call =>
            {
                var removed = call.Arg<SavingGoal>();
                persistedGoals.RemoveAll(goal => goal.Id == removed.Id);
            });

        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.Notifications.Returns(notificationRepository);
        unitOfWork.SavingGoals.Returns(savingGoalRepository);

        var runner = new InlineDataOperationRunner(unitOfWork);
        return new NotificationActionService(runner);
    }
}
