using AutoMapper;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Constants;
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
            Category = NotificationGroupCategory.RecurringTransactionDue,
            Notifications = [],
            Header = "Header",
            Message = "Message",
            Count = 0
        };

        AssertGroupedCardShape(card);
    }

    [Fact]
    public async Task LoadAsync_CreditDueDate_DoesNotCreateUpcomingPaymentNotification()
    {
        var dueDate = DateTime.Today.AddDays(7);
        var accounts = new List<AccountVM>
        {
            new()
            {
                Id = 1,
                Name = "Visa",
                AccountType = AccountType.Credit,
                MonthlyDueDate = dueDate.Day,
                AccountLimit = 1000m,
                SpentAmount = 0m
            }
        };

        var vm = CreateVm(
            expenses: [],
            expenseLogs: [],
            accounts: accounts,
            out _);

        await vm.LoadAsync();

        Assert.DoesNotContain(vm.Notifications, notification =>
            notification.Type.StartsWith("UpcomingPayment-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ClearAllNotificationsAsync_SetsIsCleared_ForActiveNotifications()
    {
        var vm = CreateVm(
            expenses: [],
            expenseLogs: [],
            accounts: [],
            out var persistedNotifications);
        persistedNotifications.Add(new Notification
        {
            Id = 1,
            Type = $"GoalDeadline-1_{DateTime.Today.AddDays(7):yyyyMMdd}",
            Header = "Goal Deadline - Vacation",
            Message = "Vacation is due soon.",
            CreatedOn = DateTime.Today,
            IsCleared = false,
            IsForDeletion = false
        });

        await vm.LoadAsync();
        await vm.ClearAllNotificationsCommand.ExecuteAsync(null);

        Assert.Equal(0, vm.NotificationCount);
        Assert.Single(persistedNotifications);
        Assert.True(persistedNotifications[0].IsCleared);
        Assert.False(persistedNotifications[0].IsForDeletion);
    }

    [Fact]
    public async Task SnoozeAllNotificationsAsync_UsesConfiguredPeriodAndHidesVisibleNotifications()
    {
        var vm = CreateVm(
            expenses: [],
            expenseLogs: [],
            accounts: [],
            out var persistedNotifications,
            userSettings:
            [
                new UserSettings { Name = UserSettingNames.NotificationsSnoozePeriod, Value = "12" }
            ]);
        SeedDistinctCategoryNotifications(persistedNotifications);

        await vm.LoadAsync();
        await vm.SnoozeAllNotificationsCommand.ExecuteAsync(null);

        Assert.False(vm.HasNotifications);
        Assert.All(persistedNotifications, notification =>
            Assert.True(notification.CreatedOn > DateTime.Now.AddHours(11)));
    }

    [Fact]
    public async Task LoadAsync_DoesNotRecreateSnoozedDuplicateBeforeCreatedOn()
    {
        var currentWeekday = DateTime.Today.DayOfWeek == DayOfWeek.Sunday
            ? 7
            : (int)DateTime.Today.DayOfWeek;
        var vm = CreateVm(
            expenses: [],
            expenseLogs: [],
            accounts: [],
            out var persistedNotifications,
            recurringTransactions:
            [
                new RecurringTransaction
                {
                    Id = 10,
                    Name = "Rent",
                    Amount = 1200m,
                    RecurringPeriod = RecurringPeriod.Weekly,
                    RecurringTime = currentWeekday,
                    Type = RecurringTransactionType.Expense,
                    IsEnabled = true
                }
            ],
            userSettings:
            [
                new UserSettings { Name = UserSettingNames.IsFixedExpensesDeductionNotifEnabled, Value = "true" }
            ]);

        persistedNotifications.Add(new Notification
        {
            Id = 1,
            Type = $"RecurringTransactionDue-10_{DateTime.Today:yyyyMMdd}",
            Header = "Recurring Transaction Due - Rent",
            Message = $"Rent is scheduled for {DateTime.Today:MMM d}.",
            CreatedOn = DateTime.Now.AddHours(24),
            IsCleared = false,
            IsForDeletion = false
        });

        await vm.LoadAsync();

        Assert.Empty(vm.Notifications);
        Assert.Single(persistedNotifications);
    }

    [Fact]
    public async Task LoadAsync_WithMultipleNotifications_InitializesCarouselState()
    {
        var vm = CreateVm(
            expenses: [],
            expenseLogs: [],
            accounts: [],
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
        var currentWeekday = DateTime.Today.DayOfWeek == DayOfWeek.Sunday
            ? 7
            : (int)DateTime.Today.DayOfWeek;
        var vm = CreateVm(
            expenses: [],
            expenseLogs: [],
            accounts: [],
            out _,
            recurringTransactions:
            [
                new RecurringTransaction
                {
                    Id = 1,
                    Name = "Rent",
                    Amount = 1200m,
                    SourceId = 1,
                    RecurringPeriod = RecurringPeriod.Weekly,
                    RecurringTime = currentWeekday,
                    Type = RecurringTransactionType.Income,
                    IsEnabled = true
                }
            ],
            userSettings:
            [
                new UserSettings { Name = UserSettingNames.IsFixedExpensesDeductionNotifEnabled, Value = "true" }
            ]);

        await vm.LoadAsync();

        Assert.True(vm.HasNotifications);
        Assert.NotNull(vm.CurrentNotificationItem);
        Assert.True(vm.NotificationItems.Count >= 1);
    }

    [Fact]
    public async Task LoadAsync_BudgetThresholdUsesTypedAllocationAndKeepsNotificationSettingsFromUserSettings()
    {
        var currentWeekday = DateTime.Today.DayOfWeek == DayOfWeek.Sunday
            ? 7
            : (int)DateTime.Today.DayOfWeek;
        var vm = CreateVm(
            expenses: [],
            expenseLogs:
            [
                new ExpenseLogVM
                {
                    Id = 1,
                    Amount = 380m,
                    DeductedOn = DateTime.Today,
                    Expense = new ExpenseVM
                    {
                        Id = 1,
                        Name = "Rent",
                        ExpenseCategory = ExpenseCategory.Needs
                    }
                }
            ],
            accounts:
            [
                new AccountVM
                {
                    Id = 1,
                    Name = "Checking",
                    AccountType = AccountType.Checking,
                    Balance = 1_000m,
                    IsEnabled = true
                }
            ],
            out _,
            recurringTransactions:
            [
                new RecurringTransaction
                {
                    Id = 10,
                    Name = "Recurring Rent",
                    Amount = 100m,
                    RecurringPeriod = RecurringPeriod.Weekly,
                    RecurringTime = currentWeekday,
                    Type = RecurringTransactionType.Expense,
                    IsEnabled = true
                }
            ],
            userSettings:
            [
                new UserSettings { Name = UserSettingNames.IsFixedExpensesDeductionNotifEnabled, Value = "true" }
            ],
            budgetAllocation: new BudgetAllocation
            {
                NeedsThreshold = 40,
                WantsThreshold = 40,
                InvestThreshold = 20
            });

        await vm.LoadAsync();

        Assert.Contains(vm.Notifications, notification =>
            notification.Type == "BudgetThresholdNeeds" &&
            notification.Message == "Needs has reached 95% of its allocation.");
        Assert.Contains(vm.Notifications, notification =>
            notification.Type.StartsWith("RecurringTransactionDue-10_", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadAsync_TagSpendingLimitExceeded_DoesNotCreateTagLimitNotification()
    {
        var groceriesTag = new ExpenseTagVM
        {
            Id = 11,
            Name = "Groceries",
            HexCode = "#3FE0A1",
            SpendingLimit = 100m
        };
        var vm = CreateVm(
            expenses: [],
            expenseLogs:
            [
                new ExpenseLogVM
                {
                    Id = 1,
                    Amount = 80m,
                    DeductedOn = DateTime.Today,
                    Expense = new ExpenseVM
                    {
                        Id = 1,
                        Name = "Market",
                        ExpenseCategory = ExpenseCategory.Needs,
                        ExpenseTag = groceriesTag
                    }
                },
                new ExpenseLogVM
                {
                    Id = 2,
                    Amount = 25m,
                    DeductedOn = DateTime.Today,
                    Expense = new ExpenseVM
                    {
                        Id = 2,
                        Name = "Snacks",
                        ExpenseCategory = ExpenseCategory.Wants,
                        ExpenseTag = groceriesTag
                    }
                }
            ],
            accounts: [],
            out _);

        await vm.LoadAsync();

        Assert.DoesNotContain(vm.Notifications, notification =>
            notification.Type.StartsWith("TagSpendingLimit-11", StringComparison.Ordinal));
    }


    [Fact]
    public async Task LoadAsync_CreatesNotificationForEachRecurringTransactionDueWithinReminderWindow()
    {
        var currentWeekday = DateTime.Today.DayOfWeek == DayOfWeek.Sunday
            ? 7
            : (int)DateTime.Today.DayOfWeek;
        var useMonthlyDueDate = DateTime.Today.Day <= 28;
        var recurringPeriod = useMonthlyDueDate ? RecurringPeriod.Monthly : RecurringPeriod.Weekly;
        var recurringTime = useMonthlyDueDate ? DateTime.Today.Day : currentWeekday;
        var recurringTransactions = new[]
        {
            new RecurringTransaction
            {
                Id = 1,
                Name = "Rent",
                Amount = 1200m,
                RecurringPeriod = recurringPeriod,
                RecurringTime = recurringTime,
                Type = RecurringTransactionType.Expense,
                IsEnabled = true
            },
            new RecurringTransaction
            {
                Id = 2,
                Name = "Utilities",
                Amount = 150m,
                RecurringPeriod = recurringPeriod,
                RecurringTime = recurringTime,
                Type = RecurringTransactionType.Expense,
                IsEnabled = true
            }
        };
        var userSettings = new[]
        {
            new UserSettings { Name = UserSettingNames.IsFixedExpensesDeductionNotifEnabled, Value = "true" },
            new UserSettings { Name = UserSettingNames.DeadlineReminderDays, Value = "7" }
        };
        var vm = CreateVm(
            expenses: [],
            expenseLogs: [],
            accounts: [],
            out _,
            recurringTransactions: recurringTransactions,
            userSettings: userSettings);

        await vm.LoadAsync();

        var recurringNotifications = vm.Notifications
            .Where(notification => notification.Type.StartsWith("RecurringTransactionDue-", StringComparison.Ordinal))
            .ToList();
        Assert.Equal(2, recurringNotifications.Count);
        Assert.Contains(recurringNotifications, notification => notification.Header == "Recurring Transaction Due - Rent");
        Assert.Contains(recurringNotifications, notification => notification.Header == "Recurring Transaction Due - Utilities");
    }

    [Fact]
    public async Task NavigateNextCommand_WrapsAndUpdatesCurrentNotification()
    {
        var vm = CreateVm(
            expenses: [],
            expenseLogs: [],
            accounts: [],
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
            accounts: [],
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
            accounts:
            [
                new AccountVM
                {
                    Id = 1,
                    Name = "Visa",
                    AccountType = AccountType.Credit,
                    MonthlyDueDate = dueDate.Day,
                    AccountLimit = 1000m,
                    SpentAmount = 250m
                },
                new AccountVM
                {
                    Id = 2,
                    Name = "MasterCard",
                    AccountType = AccountType.Credit,
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
            accounts: [],
            out var persistedNotifications);

        SeedDistinctCategoryNotifications(persistedNotifications);

        await vm.LoadAsync();

        var cardToClear = Assert.Single(vm.NotificationItems.Where(item =>
            item.Category == NotificationGroupCategory.GoalDeadline));

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
            accounts: [],
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
    public async Task OpenNotificationActionAsync_RecurringTransactionDue_ForwardsActionDecisionsToService()
    {
        var currentWeekday = DateTime.Today.DayOfWeek == DayOfWeek.Sunday
            ? 7
            : (int)DateTime.Today.DayOfWeek;
        var actionService = Substitute.For<INotificationActionService>();
        IReadOnlyCollection<NotificationChecklistActionDecision>? capturedDecisions = null;
        actionService.ExecuteChecklistActionAsync(
                Arg.Any<NotificationItemVM>(),
                Arg.Any<IReadOnlyCollection<NotificationChecklistActionDecision>>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                capturedDecisions = call.ArgAt<IReadOnlyCollection<NotificationChecklistActionDecision>>(1);
                return Task.FromResult(false);
            });

        var dialogService = Substitute.For<IDialogService>();
        dialogService.ShowNotificationChecklistAction(
                Arg.Any<NotificationChecklistActionVM>(),
                Arg.Any<System.Windows.Window?>())
            .Returns(call =>
            {
                var checklistVm = call.ArgAt<NotificationChecklistActionVM>(0);
                checklistVm.Items[0].SelectedAction = NotificationChecklistItemActionType.Process;
                return checklistVm.ProcessAsync().GetAwaiter().GetResult();
            });

        var vm = CreateVm(
            expenses: [],
            expenseLogs: [],
            accounts:
            [
                new AccountVM
                {
                    Id = 1,
                    Name = "Checking",
                    AccountType = AccountType.Checking,
                    Balance = 1000m,
                    IsEnabled = true
                }
            ],
            out _,
            actionService,
            dialogService,
            recurringTransactions:
            [
                new RecurringTransaction
                {
                    Id = 10,
                    Name = "Rent",
                    Amount = 1200m,
                    SourceId = 1,
                    RecurringPeriod = RecurringPeriod.Weekly,
                    RecurringTime = currentWeekday,
                    Type = RecurringTransactionType.Income,
                    IsEnabled = true
                }
            ],
            userSettings:
            [
                new UserSettings { Name = UserSettingNames.IsFixedExpensesDeductionNotifEnabled, Value = "true" }
            ]);

        await vm.LoadAsync();

        var recurringDueCard = Assert.Single(vm.NotificationItems.Where(item =>
            item.Category == NotificationGroupCategory.RecurringTransactionDue));

        await vm.OpenNotificationActionCommand.ExecuteAsync(recurringDueCard);

        await actionService.Received(1).ExecuteChecklistActionAsync(
            Arg.Is<NotificationItemVM>(card => card.Category == NotificationGroupCategory.RecurringTransactionDue),
            Arg.Any<IReadOnlyCollection<NotificationChecklistActionDecision>>(),
            Arg.Any<CancellationToken>());
        var decision = Assert.Single(capturedDecisions!);
        Assert.Equal(10, decision.EntityId);
        Assert.Equal(NotificationChecklistItemActionType.Process, decision.Action);
        Assert.Equal(1, decision.SelectedSourceId);
    }

    [Fact]
    public async Task OpenNotificationActionAsync_AppUpdate_ParsesPayloadAndForwardsToInteractionService()
    {
        var interactionService = Substitute.For<IAppUpdateInteractionService>();
        var vm = CreateVm(
            expenses: [],
            expenseLogs: [],
            accounts: [],
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
            accounts: [],
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
            accounts: [],
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
            accounts: [],
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
            accounts: [],
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
            accounts: [],
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
    public async Task LoadAsync_MarksUpcomingPaymentNotificationForDeletion_WhenDueDateIsStillUpcoming()
    {
        var vm = CreateVm(
            expenses: [],
            expenseLogs: [],
            accounts: [],
            out var persistedNotifications);

        persistedNotifications.Add(new Notification
        {
            Id = 1,
            Type = $"UpcomingPayment-1_{DateTime.Today.AddDays(7):yyyyMMdd}",
            Header = "Upcoming Payment - Visa",
            Message = "Visa is due soon.",
            CreatedOn = DateTime.Today.AddDays(-1),
            IsCleared = false,
            IsForDeletion = false
        });

        await vm.LoadAsync();

        Assert.True(persistedNotifications[0].IsForDeletion);
        Assert.DoesNotContain(vm.Notifications, notification =>
            notification.Type.StartsWith("UpcomingPayment-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadAsync_MarksGoalDeadlineNotificationForDeletion_WhenSavingEndDatePassed()
    {
        var vm = CreateVm(
            expenses: [],
            expenseLogs: [],
            accounts: [],
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
        var accounts = new List<AccountVM>
        {
            new()
            {
                Id = 1,
                Name = "Visa",
                AccountType = AccountType.Credit,
                MonthlyDueDate = 10,
                AccountLimit = 1000m,
                SpentAmount = 0m
            }
        };

        var vm = CreateVm(
            expenses: [],
            expenseLogs: [],
            accounts: accounts,
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
    public async Task LoadAsync_MarksRecurringTransactionNotificationForDeletion_WhenTransactionNoLongerExists()
    {
        var vm = CreateVm(
            expenses: [],
            expenseLogs: [],
            accounts: [],
            out var persistedNotifications,
            recurringTransactions: [],
            userSettings:
            [
                new UserSettings { Name = UserSettingNames.IsFixedExpensesDeductionNotifEnabled, Value = "true" }
            ]);

        persistedNotifications.Add(new Notification
        {
            Id = 1,
            Type = $"RecurringTransactionDue-42_{DateTime.Today:yyyyMMdd}",
            Header = "Recurring Transaction Due - Removed",
            Message = "Removed is scheduled today.",
            CreatedOn = DateTime.Today,
            IsCleared = false,
            IsForDeletion = false
        });

        await vm.LoadAsync();

        Assert.True(persistedNotifications[0].IsForDeletion);
        Assert.DoesNotContain(vm.Notifications, notification =>
            notification.Type.StartsWith("RecurringTransactionDue-42_", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadAsync_MarksRecurringTransactionNotificationForDeletion_WhenTransactionIsDisabled()
    {
        var vm = CreateVm(
            expenses: [],
            expenseLogs: [],
            accounts: [],
            out var persistedNotifications,
            recurringTransactions:
            [
                new RecurringTransaction
                {
                    Id = 42,
                    Name = "Disabled Subscription",
                    Amount = 9.99m,
                    RecurringPeriod = RecurringPeriod.Monthly,
                    RecurringTime = Math.Min(DateTime.Today.Day, 28),
                    Type = RecurringTransactionType.Expense,
                    IsEnabled = false
                }
            ],
            userSettings:
            [
                new UserSettings { Name = UserSettingNames.IsFixedExpensesDeductionNotifEnabled, Value = "true" }
            ]);

        persistedNotifications.Add(new Notification
        {
            Id = 1,
            Type = $"RecurringTransactionDue-42_{DateTime.Today:yyyyMMdd}",
            Header = "Recurring Transaction Due - Disabled Subscription",
            Message = "Disabled Subscription is scheduled today.",
            CreatedOn = DateTime.Today,
            IsCleared = false,
            IsForDeletion = false
        });

        await vm.LoadAsync();

        Assert.True(persistedNotifications[0].IsForDeletion);
        Assert.DoesNotContain(vm.Notifications, notification =>
            notification.Type.StartsWith("RecurringTransactionDue-42_", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadAsync_MarksClearedNotificationForDeletion_ForNonSpecialTypes()
    {
        var vm = CreateVm(
            expenses: [],
            expenseLogs: [],
            accounts: [],
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

    [Fact]
    public void ResolveRecurringTransactionDueDate_None_ReturnsNull()
    {
        var transaction = new RecurringTransactionVM
        {
            RecurringPeriod = RecurringPeriod.None,
            RecurringTime = 0
        };

        var dueDate = NotificationPanelVM.ResolveRecurringTransactionDueDate(transaction, new DateTime(2026, 5, 18));

        Assert.Null(dueDate);
    }

    [Theory]
    [InlineData(RecurringPeriod.Weekly)]
    [InlineData(RecurringPeriod.Biweekly)]
    public void ResolveRecurringTransactionDueDate_WeeklyPeriods_UseSelectedWeekday(RecurringPeriod period)
    {
        var transaction = new RecurringTransactionVM
        {
            RecurringPeriod = period,
            RecurringTime = 5
        };

        var dueDate = NotificationPanelVM.ResolveRecurringTransactionDueDate(transaction, new DateTime(2026, 5, 18));

        Assert.Equal(new DateTime(2026, 5, 22), dueDate);
    }

    [Fact]
    public void ResolveRecurringTransactionDueDate_Monthly_UsesMonthlyDay()
    {
        var transaction = new RecurringTransactionVM
        {
            RecurringPeriod = RecurringPeriod.Monthly,
            RecurringTime = 28
        };

        var dueDate = NotificationPanelVM.ResolveRecurringTransactionDueDate(transaction, new DateTime(2026, 5, 18));

        Assert.Equal(new DateTime(2026, 5, 28), dueDate);
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
                Type = $"GoalDeadline-1_{DateTime.Today.AddDays(7):yyyyMMdd}",
                Header = "Goal Deadline - Vacation",
                Message = "Vacation is due soon.",
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
        IReadOnlyList<AccountVM> accounts,
        out List<Notification> persistedNotifications,
        INotificationActionService? notificationActionService = null,
        IDialogService? dialogService = null,
        IAppUpdateInteractionService? appUpdateInteractionService = null,
        IReadOnlyList<RecurringTransaction>? recurringTransactions = null,
        IReadOnlyList<UserSettings>? userSettings = null,
        BudgetAllocation? budgetAllocation = null)
    {
        var expenseService = Substitute.For<IExpenseService>();
        expenseService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ExpenseDto>>([]));

        var expenseLogService = Substitute.For<IExpenseLogService>();
        expenseLogService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ExpenseLogDto>>([]));

        var accountService = Substitute.For<IAccountService>();
        accountService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<AccountDto>>([]));

        var userSettingsRepository = Substitute.For<IUserSettingsRepository>();
        userSettingsRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(userSettings ?? []));

        var recurringTransactionStore = recurringTransactions?.ToList() ?? [];
        var recurringTransactionRepository = Substitute.For<IRecurringTransactionRepository>();
        recurringTransactionRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<RecurringTransaction>>(recurringTransactionStore.ToList()));

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
        var expenseTagRepository = Substitute.For<IExpenseTagRepository>();
        expenseTagRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ExpenseTag>>([]));
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.UserSettings.Returns(userSettingsRepository);
        var budgetAllocationRepository = Substitute.For<IBudgetAllocationRepository>();
        budgetAllocationRepository.GetAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<BudgetAllocation?>(budgetAllocation ?? new BudgetAllocation()));
        unitOfWork.BudgetAllocation.Returns(budgetAllocationRepository);
        unitOfWork.RecurringTransactions.Returns(recurringTransactionRepository);
        unitOfWork.Notifications.Returns(notificationRepository);
        unitOfWork.SavingGoals.Returns(savingGoalRepository);
        unitOfWork.ExpenseTags.Returns(expenseTagRepository);
        var dataOperationRunner = new InlineDataOperationRunner(unitOfWork);

        var mapper = Substitute.For<IMapper>();
        var recurringDtos = recurringTransactionStore
            .Select(transaction => new RecurringTransactionDto
            {
                Id = transaction.Id,
                Name = transaction.Name,
                Amount = transaction.Amount,
                RecurringPeriod = transaction.RecurringPeriod,
                RecurringTime = transaction.RecurringTime,
                Type = transaction.Type,
                IsEnabled = transaction.IsEnabled
            })
            .ToList();
        var recurringVms = recurringTransactionStore
            .Select(transaction => new RecurringTransactionVM
            {
                Id = transaction.Id,
                Name = transaction.Name,
                Amount = transaction.Amount,
                RecurringPeriod = transaction.RecurringPeriod,
                RecurringTime = transaction.RecurringTime,
                Type = transaction.Type,
                Source = accounts.FirstOrDefault(source => source.Id == transaction.SourceId) ?? new AccountVM(),
                IsEnabled = transaction.IsEnabled
            })
            .ToList();
        mapper.Map<IReadOnlyList<ExpenseVM>>(Arg.Any<object>()).Returns(expenses);
        mapper.Map<IReadOnlyList<ExpenseLogVM>>(Arg.Any<object>()).Returns(expenseLogs);
        mapper.Map<IReadOnlyList<AccountVM>>(Arg.Any<object>()).Returns(accounts);
        mapper.Map<IReadOnlyList<RecurringTransactionDto>>(Arg.Any<object>()).Returns(recurringDtos);
        mapper.Map<IReadOnlyList<RecurringTransactionVM>>(Arg.Any<object>()).Returns(recurringVms);
        mapper.Map<IReadOnlyList<ExpenseTagDto>>(Arg.Any<object>()).Returns([]);
        mapper.Map<IReadOnlyList<ExpenseTagVM>>(Arg.Any<object>()).Returns([]);
        mapper.Map<IReadOnlyList<SavingGoalDto>>(Arg.Any<object>()).Returns([]);
        mapper.Map<IReadOnlyList<SavingGoalVM>>(Arg.Any<object>()).Returns([]);

        return new NotificationPanelVM(
            expenseService,
            expenseLogService,
            accountService,
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
