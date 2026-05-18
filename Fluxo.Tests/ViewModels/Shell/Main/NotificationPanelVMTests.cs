using AutoMapper;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.DTO;
using Fluxo.Core.Enums;
using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Services.Dialogs;
using Fluxo.Services.Notifications;
using Fluxo.Services.Updates;
using Fluxo.Tests.TestDoubles;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Popups;
using Fluxo.ViewModels.Shell;
using Fluxo.ViewModels.Shell.Main;
using NSubstitute;
using System.Text;
using Xunit;

namespace Fluxo.Tests.ViewModels.Shell.Main;

public class NotificationPanelVMTests
{
    [Fact]
    public void GroupedCardShape_ExposesRequiredMembers()
    {
        var card = new NotificationItemVM
        {
            Category = NotificationGroupCategory.FixedExpenseDue,
            Notifications = [],
            Header = "Header",
            Message = "Message",
            Count = 0
        };

        AssertGroupedCardShape(card);
    }

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
                SpentAmount = 0m
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
                SpentAmount = 0m
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
        var vm = CreateVm(
            expenses: [],
            expenseLogs: [],
            spendingSources: [],
            out var persistedNotifications);

        SeedDistinctCategoryNotifications(persistedNotifications);

        await vm.LoadAsync();

        Assert.True(vm.HasNotifications);
        Assert.True(vm.HasMultipleNotifications);
        Assert.Equal(2, vm.NotificationStepCount);
        Assert.Equal(1, vm.CurrentStepNumber);
        Assert.NotNull(vm.CurrentNotificationItem);
        Assert.NotNull(vm.CurrentNotification);
        Assert.Equal(0, vm.CurrentNotificationIndex);
    }

    [Fact]
    public async Task LoadAsync_GroupsNotificationsIntoCards()
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
        Assert.NotNull(vm.CurrentNotificationItem);
        Assert.True(vm.NotificationItems.Count >= 1);
    }

    [Fact]
    public async Task NavigateNextCommand_WrapsAndUpdatesCurrentNotification()
    {
        var vm = CreateVm(
            expenses: [],
            expenseLogs: [],
            spendingSources: [],
            out var persistedNotifications);

        SeedDistinctCategoryNotifications(persistedNotifications);

        await vm.LoadAsync();

        vm.NavigateNextCommand.Execute(null);
        Assert.Equal(1, vm.CurrentNotificationIndex);
        Assert.Equal(2, vm.CurrentStepNumber);
        Assert.Equal(-1, vm.NavigationDirection);
        Assert.NotNull(vm.CurrentNotificationItem);

        vm.NavigateNextCommand.Execute(null);
        Assert.Equal(0, vm.CurrentNotificationIndex);
        Assert.Equal(1, vm.CurrentStepNumber);
        Assert.Equal(-1, vm.NavigationDirection);
    }

    [Fact]
    public async Task NavigatePreviousCommand_WrapsToLastNotification()
    {
        var vm = CreateVm(
            expenses: [],
            expenseLogs: [],
            spendingSources: [],
            out var persistedNotifications);

        SeedDistinctCategoryNotifications(persistedNotifications);

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
        Assert.Null(vm.CurrentNotificationItem);
        Assert.Null(vm.CurrentNotification);
    }

    [Fact]
    public async Task ClearNotificationGroupAsync_SetsIsCleared_ForSelectedGroupOnly()
    {
        var vm = CreateVm(
            expenses: [],
            expenseLogs: [],
            spendingSources: [],
            out var persistedNotifications);

        SeedDistinctCategoryNotifications(persistedNotifications);

        await vm.LoadAsync();

        var cardToClear = Assert.Single(vm.NotificationItems.Where(item =>
            item.Category == NotificationGroupCategory.UpcomingPayment));

        await vm.ClearNotificationGroupCommand.ExecuteAsync(cardToClear);

        Assert.Single(persistedNotifications.Where(notification => notification.IsCleared));
        Assert.Single(persistedNotifications.Where(notification =>
            !notification.IsCleared && notification.Type.StartsWith("LowBalance", StringComparison.Ordinal)));
        Assert.Single(vm.NotificationItems);
        Assert.Equal(NotificationGroupCategory.LowBalance, vm.NotificationItems[0].Category);
    }

    [Fact]
    public async Task OpenNotificationActionAsync_NonActionableCategory_DoesNotMutateNotifications()
    {
        var vm = CreateVm(
            expenses: [],
            expenseLogs: [],
            spendingSources: [],
            out var persistedNotifications);

        SeedDistinctCategoryNotifications(persistedNotifications);

        await vm.LoadAsync();

        var lowBalanceCard = Assert.Single(vm.NotificationItems.Where(item =>
            item.Category == NotificationGroupCategory.LowBalance));

        await vm.OpenNotificationActionCommand.ExecuteAsync(lowBalanceCard);

        Assert.Equal(2, persistedNotifications.Count);
        Assert.DoesNotContain(persistedNotifications, notification => notification.IsCleared);
        Assert.Equal(2, vm.NotificationItems.Count);
    }

    [Fact]
    public async Task OpenNotificationActionAsync_UpcomingPayment_ForwardsActionDecisionsToService()
    {
        var dueDate = DateTime.Today.AddDays(7);
        var actionService = Substitute.For<INotificationActionService>();
        actionService.ExecuteChecklistActionAsync(
                Arg.Any<NotificationItemVM>(),
                Arg.Any<IReadOnlyCollection<NotificationChecklistActionDecision>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        var dialogService = Substitute.For<IDialogService>();
        dialogService.ShowNotificationChecklistAction(
                Arg.Any<NotificationChecklistActionVM>(),
                Arg.Any<System.Windows.Window?>())
            .Returns(call =>
            {
                var checklistVm = call.ArgAt<NotificationChecklistActionVM>(0);
                checklistVm.Items[0].SelectedAction = NotificationChecklistItemActionType.Paid;
                checklistVm.ProceedCommand.Execute(null);
                return true;
            });

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
                    SpentAmount = 0m
                }
            ],
            out _,
            actionService,
            dialogService);

        await vm.LoadAsync();

        var upcomingPaymentCard = Assert.Single(vm.NotificationItems.Where(item =>
            item.Category == NotificationGroupCategory.UpcomingPayment));

        await vm.OpenNotificationActionCommand.ExecuteAsync(upcomingPaymentCard);

        await actionService.Received(1).ExecuteChecklistActionAsync(
            Arg.Is<NotificationItemVM>(card => card.Category == NotificationGroupCategory.UpcomingPayment),
            Arg.Is<IReadOnlyCollection<NotificationChecklistActionDecision>>(decisions =>
                decisions.Count == 1 &&
                decisions.First().EntityId == 1 &&
                decisions.First().Action == NotificationChecklistItemActionType.Paid &&
                decisions.First().SelectedSourceId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OpenNotificationActionAsync_AppUpdate_ParsesPayloadAndForwardsToInteractionService()
    {
        var interactionService = Substitute.For<IAppUpdateInteractionService>();
        var vm = CreateVm(
            expenses: [],
            expenseLogs: [],
            spendingSources: [],
            out var persistedNotifications,
            appUpdateInteractionService: interactionService);

        persistedNotifications.Add(new Notification
        {
            Id = 1,
            Type = BuildAppUpdateType(
                "3.4.5",
                "fluxo-3.4.5-Installer.exe",
                "https://example.test/fluxo-3.4.5-Installer.exe"),
            Header = "New Update Found",
            Message = "Version 3.4.5 is available for download",
            CreatedOn = DateTime.Today,
            IsCleared = false,
            IsForDeletion = false
        });

        await vm.LoadAsync();

        var appUpdateCard = Assert.Single(vm.NotificationItems.Where(item =>
            item.Category == NotificationGroupCategory.AppUpdate));

        await vm.OpenNotificationActionCommand.ExecuteAsync(appUpdateCard);

        await interactionService.Received(1).HandleAvailableUpdateAsync(
            Arg.Is<AppUpdateCheckResult>(update =>
                update.Status == AppUpdateCheckStatus.UpdateAvailable
                && update.LatestVersion == "3.4.5"
                && update.InstallerAssetName == "fluxo-3.4.5-Installer.exe"
                && update.InstallerDownloadUrl == "https://example.test/fluxo-3.4.5-Installer.exe"),
            null);
    }

    [Fact]
    public async Task OpenNotificationActionAsync_AppUpdate_WithLegacyPayload_ForwardsToInteractionService()
    {
        var interactionService = Substitute.For<IAppUpdateInteractionService>();
        var vm = CreateVm(
            expenses: [],
            expenseLogs: [],
            spendingSources: [],
            out var persistedNotifications,
            appUpdateInteractionService: interactionService);

        persistedNotifications.Add(new Notification
        {
            Id = 1,
            Type = "AppUpdate-2.9.1",
            Header = "New Update Found",
            Message = "Version 2.9.1 is available for download",
            CreatedOn = DateTime.Today,
            IsCleared = false,
            IsForDeletion = false
        });

        await vm.LoadAsync();

        var appUpdateCard = Assert.Single(vm.NotificationItems.Where(item =>
            item.Category == NotificationGroupCategory.AppUpdate));

        await vm.OpenNotificationActionCommand.ExecuteAsync(appUpdateCard);

        await interactionService.Received(1).HandleAvailableUpdateAsync(
            Arg.Is<AppUpdateCheckResult>(update =>
                update.Status == AppUpdateCheckStatus.UpdateAvailable
                && update.LatestVersion == "2.9.1"
                && update.InstallerAssetName == string.Empty
                && update.InstallerDownloadUrl == string.Empty),
            null);
    }

    [Fact]
    public async Task OpenNotificationActionAsync_AppUpdate_WithMalformedPayload_DoesNotDispatch()
    {
        var interactionService = Substitute.For<IAppUpdateInteractionService>();
        var vm = CreateVm(
            expenses: [],
            expenseLogs: [],
            spendingSources: [],
            out var persistedNotifications,
            appUpdateInteractionService: interactionService);

        persistedNotifications.Add(new Notification
        {
            Id = 1,
            Type = "AppUpdate-invalid.invalid.invalid",
            Header = "New Update Found",
            Message = "Version invalid is available for download",
            CreatedOn = DateTime.Today,
            IsCleared = false,
            IsForDeletion = false
        });

        await vm.LoadAsync();

        var appUpdateCard = Assert.Single(vm.NotificationItems.Where(item =>
            item.Category == NotificationGroupCategory.AppUpdate));

        await vm.OpenNotificationActionCommand.ExecuteAsync(appUpdateCard);

        await interactionService
            .DidNotReceive()
            .HandleAvailableUpdateAsync(Arg.Any<AppUpdateCheckResult>(), Arg.Any<System.Windows.Window?>());
    }

    [Fact]
    public async Task OpenNotificationActionAsync_AppUpdate_WithBadMetadataTokenCount_DoesNotDispatch()
    {
        var interactionService = Substitute.For<IAppUpdateInteractionService>();
        var vm = CreateVm(
            expenses: [],
            expenseLogs: [],
            spendingSources: [],
            out var persistedNotifications,
            appUpdateInteractionService: interactionService);

        persistedNotifications.Add(new Notification
        {
            Id = 1,
            Type = "AppUpdate-bad.token-count",
            Header = "New Update Found",
            Message = "Version bad is available for download",
            CreatedOn = DateTime.Today,
            IsCleared = false,
            IsForDeletion = false
        });

        await vm.LoadAsync();

        var appUpdateCard = Assert.Single(vm.NotificationItems.Where(item =>
            item.Category == NotificationGroupCategory.AppUpdate));

        await vm.OpenNotificationActionCommand.ExecuteAsync(appUpdateCard);

        await interactionService
            .DidNotReceive()
            .HandleAvailableUpdateAsync(Arg.Any<AppUpdateCheckResult>(), Arg.Any<System.Windows.Window?>());
    }

    [Fact]
    public async Task ClearAllNotificationsAsync_WithGroupedItems_ClearsAllPersistedActiveRows()
    {
        var vm = CreateVm(
            expenses: [],
            expenseLogs: [],
            spendingSources: [],
            out var persistedNotifications);

        SeedDistinctCategoryNotifications(persistedNotifications);

        await vm.LoadAsync();
        await vm.ClearAllNotificationsCommand.ExecuteAsync(null);

        Assert.Equal(2, persistedNotifications.Count);
        Assert.All(persistedNotifications, notification => Assert.True(notification.IsCleared));
        Assert.Empty(vm.NotificationItems);
        Assert.False(vm.HasNotifications);
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

    private static void AssertGroupedCardShape(NotificationItemVM card)
    {
        Assert.NotEqual(NotificationGroupCategory.Other, card.Category);
        Assert.True(card.Count >= 0);
        Assert.NotNull(card.Notifications);
        Assert.NotNull(card.Header);
        Assert.NotNull(card.Message);
    }

    private static void SeedDistinctCategoryNotifications(List<Notification> persistedNotifications)
    {
        persistedNotifications.AddRange(
        [
            new Notification
            {
                Id = 1,
                Type = $"UpcomingPayment-1_{DateTime.Today.AddDays(7):yyyyMMdd}",
                Header = "Upcoming Payment - Visa",
                Message = "Visa is due soon.",
                CreatedOn = DateTime.Today.AddMinutes(-1),
                IsCleared = false,
                IsForDeletion = false
            },
            new Notification
            {
                Id = 2,
                Type = "LowBalance-9",
                Header = "Low Balance - Wallet",
                Message = "Wallet is down to 20%.",
                CreatedOn = DateTime.Today.AddMinutes(-2),
                IsCleared = false,
                IsForDeletion = false
            }
        ]);
    }

    private static NotificationPanelVM CreateVm(
        IReadOnlyList<ExpenseVM> expenses,
        IReadOnlyList<ExpenseLogVM> expenseLogs,
        IReadOnlyList<SpendingSourceVM> spendingSources,
        out List<Notification> persistedNotifications,
        INotificationActionService? notificationActionService = null,
        IDialogService? dialogService = null,
        IAppUpdateInteractionService? appUpdateInteractionService = null)
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
            new NotificationGroupingService(),
            notificationActionService,
            dialogService,
            appUpdateInteractionService: appUpdateInteractionService);
    }

    private static string BuildAppUpdateType(string version, string installerAssetName, string installerDownloadUrl)
    {
        return $"AppUpdate-{EncodeToken(version)}.{EncodeToken(installerAssetName)}.{EncodeToken(installerDownloadUrl)}";
    }

    private static string EncodeToken(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
