using AutoMapper;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.DTO;
using Fluxo.Core.Enums;
using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Tests.TestDoubles;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Shell;
using Fluxo.ViewModels.Shell.Main;
using NSubstitute;
using Xunit;

namespace Fluxo.Tests.ViewModels.Shell.Main;

public class NotificationPanelVMTests
{
    [Fact]
    public async Task LoadAsync_WhenCalledTwice_DoesNotDuplicateSystemNotifications()
    {
        var dueDate = DateTime.Today.AddDays(7);
        var spendingSources = new List<SpendingSourceVM>
        {
            new()
            {
                Id = 1,
                Name = "Visa",
                SpendingSourceType = SpendingSourceType.Credit,
                MonthlyDueDate = dueDate.Day,
                AccountLimit = 1000m,
                SpentAmount = 250m
            }
        };

        var vm = CreateVm(
            expenses: [],
            expenseLogs: [],
            spendingSources: spendingSources,
            out _);

        await vm.LoadAsync();
        await vm.LoadAsync();

        Assert.Equal(1, vm.NotificationCount);
        Assert.Equal(1, vm.Notifications.Select(n => n.Type).Distinct().Count());
    }

    [Fact]
    public async Task ClearAllNotificationsAsync_SetsIsCleared_ForUpcomingPaymentNotifications()
    {
        var dueDate = DateTime.Today.AddDays(7);
        var spendingSources = new List<SpendingSourceVM>
        {
            new()
            {
                Id = 1,
                Name = "Visa",
                SpendingSourceType = SpendingSourceType.Credit,
                MonthlyDueDate = dueDate.Day,
                AccountLimit = 1000m,
                SpentAmount = 250m
            }
        };

        var vm = CreateVm(
            expenses: [],
            expenseLogs: [],
            spendingSources: spendingSources,
            out var persistedNotifications);

        await vm.LoadAsync();
        await vm.ClearAllNotificationsCommand.ExecuteAsync(null);

        Assert.Equal(0, vm.NotificationCount);
        Assert.Single(persistedNotifications);
        Assert.True(persistedNotifications[0].IsCleared);
        Assert.False(persistedNotifications[0].IsForDeletion);
    }

    [Fact]
    public async Task LoadAsync_WithMultipleNotifications_InitializesCarouselState()
    {
        var dueDate = DateTime.Today.AddDays(7);
        var vm = CreateVm(
            expenses: [],
            expenseLogs: [],
            spendingSources:
            [
                new SpendingSourceVM
                {
                    Id = 1,
                    Name = "Visa",
                    SpendingSourceType = SpendingSourceType.Credit,
                    MonthlyDueDate = dueDate.Day,
                    AccountLimit = 1000m,
                    SpentAmount = 250m
                },
                new SpendingSourceVM
                {
                    Id = 2,
                    Name = "MasterCard",
                    SpendingSourceType = SpendingSourceType.Credit,
                    MonthlyDueDate = dueDate.Day,
                    AccountLimit = 1200m,
                    SpentAmount = 300m
                }
            ],
            out _);

        await vm.LoadAsync();

        Assert.True(vm.HasNotifications);
        Assert.True(vm.HasMultipleNotifications);
        Assert.Equal(2, vm.NotificationStepCount);
        Assert.Equal(1, vm.CurrentStepNumber);
        Assert.NotNull(vm.CurrentNotification);
        Assert.Equal(0, vm.CurrentNotificationIndex);
    }

    [Fact]
    public async Task NavigateNextCommand_WrapsAndUpdatesCurrentNotification()
    {
        var dueDate = DateTime.Today.AddDays(7);
        var vm = CreateVm(
            expenses: [],
            expenseLogs: [],
            spendingSources:
            [
                new SpendingSourceVM
                {
                    Id = 1,
                    Name = "Visa",
                    SpendingSourceType = SpendingSourceType.Credit,
                    MonthlyDueDate = dueDate.Day,
                    AccountLimit = 1000m,
                    SpentAmount = 250m
                },
                new SpendingSourceVM
                {
                    Id = 2,
                    Name = "MasterCard",
                    SpendingSourceType = SpendingSourceType.Credit,
                    MonthlyDueDate = dueDate.Day,
                    AccountLimit = 1200m,
                    SpentAmount = 300m
                }
            ],
            out _);

        await vm.LoadAsync();

        vm.NavigateNextCommand.Execute(null);
        Assert.Equal(1, vm.CurrentNotificationIndex);
        Assert.Equal(2, vm.CurrentStepNumber);
        Assert.Equal(-1, vm.NavigationDirection);

        vm.NavigateNextCommand.Execute(null);
        Assert.Equal(0, vm.CurrentNotificationIndex);
        Assert.Equal(1, vm.CurrentStepNumber);
        Assert.Equal(-1, vm.NavigationDirection);
    }

    [Fact]
    public async Task NavigatePreviousCommand_WrapsToLastNotification()
    {
        var dueDate = DateTime.Today.AddDays(7);
        var vm = CreateVm(
            expenses: [],
            expenseLogs: [],
            spendingSources:
            [
                new SpendingSourceVM
                {
                    Id = 1,
                    Name = "Visa",
                    SpendingSourceType = SpendingSourceType.Credit,
                    MonthlyDueDate = dueDate.Day,
                    AccountLimit = 1000m,
                    SpentAmount = 250m
                },
                new SpendingSourceVM
                {
                    Id = 2,
                    Name = "MasterCard",
                    SpendingSourceType = SpendingSourceType.Credit,
                    MonthlyDueDate = dueDate.Day,
                    AccountLimit = 1200m,
                    SpentAmount = 300m
                }
            ],
            out _);

        await vm.LoadAsync();

        vm.NavigatePreviousCommand.Execute(null);

        Assert.Equal(1, vm.CurrentNotificationIndex);
        Assert.Equal(2, vm.CurrentStepNumber);
        Assert.Equal(1, vm.NavigationDirection);
    }

    [Fact]
    public async Task ClearAllNotificationsAsync_ResetsCarouselState()
    {
        var dueDate = DateTime.Today.AddDays(7);
        var vm = CreateVm(
            expenses: [],
            expenseLogs: [],
            spendingSources:
            [
                new SpendingSourceVM
                {
                    Id = 1,
                    Name = "Visa",
                    SpendingSourceType = SpendingSourceType.Credit,
                    MonthlyDueDate = dueDate.Day,
                    AccountLimit = 1000m,
                    SpentAmount = 250m
                },
                new SpendingSourceVM
                {
                    Id = 2,
                    Name = "MasterCard",
                    SpendingSourceType = SpendingSourceType.Credit,
                    MonthlyDueDate = dueDate.Day,
                    AccountLimit = 1200m,
                    SpentAmount = 300m
                }
            ],
            out _);

        await vm.LoadAsync();
        await vm.ClearAllNotificationsCommand.ExecuteAsync(null);

        Assert.False(vm.HasNotifications);
        Assert.False(vm.HasMultipleNotifications);
        Assert.Equal(0, vm.NotificationStepCount);
        Assert.Equal(0, vm.CurrentStepNumber);
        Assert.Equal(-1, vm.CurrentNotificationIndex);
        Assert.Null(vm.CurrentNotification);
    }

    [Fact]
    public async Task LoadAsync_MarksUpcomingPaymentNotificationForDeletion_WhenDueDatePassed()
    {
        var vm = CreateVm(
            expenses: [],
            expenseLogs: [],
            spendingSources: [],
            out var persistedNotifications);

        persistedNotifications.Add(new Notification
        {
            Id = 1,
            Type = $"UpcomingPayment-1_{DateTime.Today.AddDays(-1):yyyyMMdd}",
            Header = "Upcoming Payment - Visa",
            Message = "Visa is due soon.",
            CreatedOn = DateTime.Today.AddDays(-5),
            IsCleared = false,
            IsForDeletion = false
        });

        await vm.LoadAsync();

        Assert.True(persistedNotifications[0].IsForDeletion);
    }

    [Fact]
    public async Task LoadAsync_MarksGoalDeadlineNotificationForDeletion_WhenSavingEndDatePassed()
    {
        var vm = CreateVm(
            expenses: [],
            expenseLogs: [],
            spendingSources: [],
            out var persistedNotifications);

        persistedNotifications.Add(new Notification
        {
            Id = 1,
            Type = $"GoalDeadline-9_{DateTime.Today.AddDays(-1):yyyyMMdd}",
            Header = "Goal Deadline - Emergency Fund",
            Message = "Emergency Fund deadline passed.",
            CreatedOn = DateTime.Today.AddDays(-3),
            IsCleared = false,
            IsForDeletion = false
        });

        await vm.LoadAsync();

        Assert.True(persistedNotifications[0].IsForDeletion);
    }

    [Fact]
    public async Task LoadAsync_MarksLatePaymentNotificationForDeletion_WhenPaymentProcessed()
    {
        var spendingSources = new List<SpendingSourceVM>
        {
            new()
            {
                Id = 1,
                Name = "Visa",
                SpendingSourceType = SpendingSourceType.Credit,
                MonthlyDueDate = 10,
                AccountLimit = 1000m,
                SpentAmount = 0m
            }
        };

        var vm = CreateVm(
            expenses: [],
            expenseLogs: [],
            spendingSources: spendingSources,
            out var persistedNotifications);

        persistedNotifications.Add(new Notification
        {
            Id = 1,
            Type = $"LatePayment-1_{DateTime.Today.AddDays(-5):yyyyMMdd}",
            Header = "Late Payment - Visa",
            Message = "Visa payment is overdue.",
            CreatedOn = DateTime.Today.AddDays(-5),
            IsCleared = false,
            IsForDeletion = false
        });

        await vm.LoadAsync();

        Assert.True(persistedNotifications[0].IsForDeletion);
    }

    [Fact]
    public async Task LoadAsync_MarksClearedNotificationForDeletion_ForNonSpecialTypes()
    {
        var vm = CreateVm(
            expenses: [],
            expenseLogs: [],
            spendingSources: [],
            out var persistedNotifications);

        persistedNotifications.Add(new Notification
        {
            Id = 1,
            Type = "BudgetThresholdNeeds",
            Header = "Budget Threshold - Needs",
            Message = "Needs threshold reached.",
            CreatedOn = DateTime.Today.AddDays(-1),
            IsCleared = true,
            IsForDeletion = false
        });

        await vm.LoadAsync();

        Assert.True(persistedNotifications[0].IsForDeletion);
    }

    private static NotificationPanelVM CreateVm(
        IReadOnlyList<ExpenseVM> expenses,
        IReadOnlyList<ExpenseLogVM> expenseLogs,
        IReadOnlyList<SpendingSourceVM> spendingSources,
        out List<Notification> persistedNotifications)
    {
        var expenseService = Substitute.For<IExpenseService>();
        expenseService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ExpenseDto>>([]));

        var expenseLogService = Substitute.For<IExpenseLogService>();
        expenseLogService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ExpenseLogDto>>([]));

        var spendingSourceService = Substitute.For<ISpendingSourceService>();
        spendingSourceService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<SpendingSourceDto>>([]));

        var userSettingsRepository = Substitute.For<IUserSettingsRepository>();
        userSettingsRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<UserSettings>>([]));

        var notificationStore = new List<Notification>();
        persistedNotifications = notificationStore;
        var notificationRepository = Substitute.For<INotificationRepository>();
        notificationRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<Notification>>(notificationStore.ToList()));
        notificationRepository.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<Notification>>(
                notificationStore.Where(notification => !notification.IsForDeletion).ToList()));
        notificationRepository
            .AddAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var notification = call.Arg<Notification>();
                notification.Id = notification.Id == 0 ? notificationStore.Count + 1 : notification.Id;
                notificationStore.Add(notification);
                return Task.CompletedTask;
            });
        notificationRepository
            .SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(0));
        notificationRepository
            .When(repo => repo.Update(Arg.Any<Notification>()))
            .Do(call =>
            {
                var updated = call.Arg<Notification>();
                var existing = notificationStore.FirstOrDefault(notification => notification.Id == updated.Id);
                if (existing is null)
                {
                    notificationStore.Add(updated);
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
        savingGoalRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<SavingGoal>>([]));
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.UserSettings.Returns(userSettingsRepository);
        unitOfWork.Notifications.Returns(notificationRepository);
        unitOfWork.SavingGoals.Returns(savingGoalRepository);
        var dataOperationRunner = new InlineDataOperationRunner(unitOfWork);

        var mapper = Substitute.For<IMapper>();
        mapper.Map<IReadOnlyList<ExpenseVM>>(Arg.Any<object>()).Returns(expenses);
        mapper.Map<IReadOnlyList<ExpenseLogVM>>(Arg.Any<object>()).Returns(expenseLogs);
        mapper.Map<IReadOnlyList<SpendingSourceVM>>(Arg.Any<object>()).Returns(spendingSources);
        mapper.Map<IReadOnlyList<SavingGoalDto>>(Arg.Any<object>()).Returns([]);
        mapper.Map<IReadOnlyList<SavingGoalVM>>(Arg.Any<object>()).Returns([]);

        return new NotificationPanelVM(
            expenseService,
            expenseLogService,
            spendingSourceService,
            dataOperationRunner,
            mapper,
            new WeakReferenceMessenger());
    }
}
