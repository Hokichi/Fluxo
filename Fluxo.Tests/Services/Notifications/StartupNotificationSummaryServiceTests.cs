using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Services.Notifications;
using Fluxo.Tests.TestDoubles;
using Fluxo.ViewModels.Shell.Main;
using NSubstitute;
using Xunit;

namespace Fluxo.Tests.Services.Notifications;

public sealed class StartupNotificationSummaryServiceTests
{
    [Fact]
    public async Task GetSummaryAsync_ReturnsNull_WhenNoActiveNotifications()
    {
        var sut = CreateSut([]);

        var summary = await sut.GetSummaryAsync();

        Assert.Null(summary);
    }

    [Fact]
    public async Task GetSummaryAsync_ReturnsGroupCountMessage_WhenMultipleGroupsExist()
    {
        var notifications = new[]
        {
            CreateNotification("UpcomingPayment-10_20260501", "Upcoming Payment - Visa"),
            CreateNotification("GoalDeadline-3_20260503", "Goal Deadline - Vacation")
        };
        var sut = CreateSut(notifications);

        var summary = await sut.GetSummaryAsync();

        Assert.NotNull(summary);
        Assert.Equal("There are 2 notifications", summary!.Message);
        Assert.Equal(2, summary.GroupCount);
    }

    [Fact]
    public async Task GetSummaryAsync_FixedExpenseDueSingular_UsesExpenseName()
    {
        var notifications = new[]
        {
            CreateNotification("UpcomingDeduction-7_20260501", "Upcoming Deduction - Rent")
        };
        var sut = CreateSut(notifications);

        var summary = await sut.GetSummaryAsync();

        Assert.NotNull(summary);
        Assert.Equal("Rent is due", summary!.Message);
        Assert.Equal(NotificationGroupCategory.FixedExpenseDue, summary.Category);
    }

    [Fact]
    public async Task GetSummaryAsync_FixedExpenseDuePlural_UsesFixedExpenseMessage()
    {
        var notifications = new[]
        {
            CreateNotification("UpcomingDeduction-7_20260501", "Upcoming Deduction - Rent"),
            CreateNotification("UpcomingDeduction-8_20260501", "Upcoming Deduction - Utilities")
        };
        var sut = CreateSut(notifications);

        var summary = await sut.GetSummaryAsync();

        Assert.NotNull(summary);
        Assert.Equal("There are 2 fixed expenses due", summary!.Message);
    }

    [Fact]
    public async Task GetSummaryAsync_UpcomingPaymentSingular_UsesSourceName()
    {
        var notifications = new[]
        {
            CreateNotification("UpcomingPayment-11_20260501", "Upcoming Payment - MasterCard")
        };
        var sut = CreateSut(notifications);

        var summary = await sut.GetSummaryAsync();

        Assert.NotNull(summary);
        Assert.Equal("MasterCard is due", summary!.Message);
    }

    [Fact]
    public async Task GetSummaryAsync_UpcomingPaymentPlural_UsesCreditCardMessage()
    {
        var notifications = new[]
        {
            CreateNotification("UpcomingPayment-11_20260501", "Upcoming Payment - MasterCard"),
            CreateNotification("UpcomingPayment-12_20260501", "Upcoming Payment - Visa")
        };
        var sut = CreateSut(notifications);

        var summary = await sut.GetSummaryAsync();

        Assert.NotNull(summary);
        Assert.Equal("There are 2 credit cards due", summary!.Message);
    }

    [Fact]
    public async Task GetSummaryAsync_GoalDeadlineSingular_UsesGoalMessage()
    {
        var notifications = new[]
        {
            CreateNotification("GoalDeadline-3_20260503", "Goal Deadline - Vacation")
        };
        var sut = CreateSut(notifications);

        var summary = await sut.GetSummaryAsync();

        Assert.NotNull(summary);
        Assert.Equal("Goal Vacation is reaching its deadline", summary!.Message);
    }

    [Fact]
    public async Task GetSummaryAsync_GoalDeadlinePlural_UsesGoalsMessage()
    {
        var notifications = new[]
        {
            CreateNotification("GoalDeadline-3_20260503", "Goal Deadline - Vacation"),
            CreateNotification("GoalDeadline-4_20260503", "Goal Deadline - Laptop")
        };
        var sut = CreateSut(notifications);

        var summary = await sut.GetSummaryAsync();

        Assert.NotNull(summary);
        Assert.Equal("There are 2 goals reaching their deadlines", summary!.Message);
    }

    [Fact]
    public async Task GetSummaryAsync_LatePaymentSingular_UsesLatePaymentSingularMessage()
    {
        var notifications = new[]
        {
            CreateNotification("LatePayment-3_20260503", "Late Payment - Visa")
        };
        var sut = CreateSut(notifications);

        var summary = await sut.GetSummaryAsync();

        Assert.NotNull(summary);
        Assert.Equal("There is one late payment due", summary!.Message);
    }

    [Fact]
    public async Task GetSummaryAsync_LatePaymentPlural_UsesLatePaymentPluralMessage()
    {
        var notifications = new[]
        {
            CreateNotification("LatePayment-3_20260503", "Late Payment - Visa"),
            CreateNotification("LatePayment-4_20260503", "Late Payment - Amex")
        };
        var sut = CreateSut(notifications);

        var summary = await sut.GetSummaryAsync();

        Assert.NotNull(summary);
        Assert.Equal("There are 2 late payments due", summary!.Message);
    }

    [Fact]
    public async Task GetSummaryAsync_DefaultFallbackSingular_UsesPrimaryHeader()
    {
        var notifications = new[]
        {
            CreateNotification("CustomType-1", "Custom Alert Header")
        };
        var sut = CreateSut(notifications);

        var summary = await sut.GetSummaryAsync();

        Assert.NotNull(summary);
        Assert.Equal("Custom Alert Header", summary!.Message);
    }

    [Fact]
    public async Task GetSummaryAsync_DefaultFallbackPlural_UsesNotificationPluralMessage()
    {
        var notifications = new[]
        {
            CreateNotification("CustomType-1", "Custom Alert Header 1"),
            CreateNotification("CustomType-2", "Custom Alert Header 2")
        };
        var sut = CreateSut(notifications);

        var summary = await sut.GetSummaryAsync();

        Assert.NotNull(summary);
        Assert.Equal("There are 2 notifications", summary!.Message);
    }

    [Fact]
    public async Task GetSummaryAsync_IgnoresClearedAndDeletedNotifications()
    {
        var notifications = new[]
        {
            CreateNotification("UpcomingPayment-11_20260501", "Upcoming Payment - MasterCard", isCleared: true),
            CreateNotification("LatePayment-4_20260503", "Late Payment - Amex", isForDeletion: true),
            CreateNotification("GoalDeadline-3_20260503", "Goal Deadline - Vacation")
        };
        var sut = CreateSut(notifications);

        var summary = await sut.GetSummaryAsync();

        Assert.NotNull(summary);
        Assert.Equal("Goal Vacation is reaching its deadline", summary!.Message);
        Assert.Equal(1, summary.GroupCount);
        Assert.Equal(1, summary.NotificationCount);
    }

    [Fact]
    public async Task GetSummaryAsync_ReturnsNull_WhenRepositoryThrows()
    {
        var notificationRepository = Substitute.For<INotificationRepository>();
        notificationRepository.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<Notification>>>(_ => throw new InvalidOperationException("boom"));

        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.Notifications.Returns(notificationRepository);

        var sut = new StartupNotificationSummaryService(
            new InlineDataOperationRunner(unitOfWork),
            new NotificationGroupingService());

        var summary = await sut.GetSummaryAsync();

        Assert.Null(summary);
    }

    [Fact]
    public async Task GetSummaryAsync_ThrowsOperationCanceledException_WhenCancellationRequested()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var notificationRepository = Substitute.For<INotificationRepository>();
        notificationRepository.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var cancellationToken = call.Arg<CancellationToken>();
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult<IReadOnlyList<Notification>>([]);
            });

        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.Notifications.Returns(notificationRepository);

        var sut = new StartupNotificationSummaryService(
            new InlineDataOperationRunner(unitOfWork),
            new NotificationGroupingService());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            sut.GetSummaryAsync(cancellationTokenSource.Token));
    }

    private static StartupNotificationSummaryService CreateSut(IReadOnlyList<Notification> notifications)
    {
        var notificationRepository = Substitute.For<INotificationRepository>();
        notificationRepository.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<Notification>>(notifications));

        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.Notifications.Returns(notificationRepository);

        return new StartupNotificationSummaryService(
            new InlineDataOperationRunner(unitOfWork),
            new NotificationGroupingService());
    }

    private static Notification CreateNotification(
        string type,
        string header,
        bool isCleared = false,
        bool isForDeletion = false)
    {
        return new Notification
        {
            Type = type,
            Header = header,
            Message = $"{header} message",
            CreatedOn = DateTime.UtcNow,
            IsCleared = isCleared,
            IsForDeletion = isForDeletion
        };
    }
}
