using AutoMapper;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.DTO;
using Fluxo.Core.Enums;
using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Core.Interfaces.Services;
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

        persistedNotifications = [];
        var notificationRepository = Substitute.For<INotificationRepository>();
        notificationRepository.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<Notification>>(
                persistedNotifications.Where(notification => !notification.IsForDeletion).ToList()));
        notificationRepository
            .AddAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var notification = call.Arg<Notification>();
                notification.Id = notification.Id == 0 ? persistedNotifications.Count + 1 : notification.Id;
                persistedNotifications.Add(notification);
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
                var existing = persistedNotifications.FirstOrDefault(notification => notification.Id == updated.Id);
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

        var mapper = Substitute.For<IMapper>();
        mapper.Map<IReadOnlyList<ExpenseVM>>(Arg.Any<object>()).Returns(expenses);
        mapper.Map<IReadOnlyList<ExpenseLogVM>>(Arg.Any<object>()).Returns(expenseLogs);
        mapper.Map<IReadOnlyList<SpendingSourceVM>>(Arg.Any<object>()).Returns(spendingSources);

        return new NotificationPanelVM(
            expenseService,
            expenseLogService,
            spendingSourceService,
            userSettingsRepository,
            notificationRepository,
            mapper,
            new WeakReferenceMessenger());
    }
}
