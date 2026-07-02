using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Enums;
using Fluxo.Resources.Resources.Messages;
using Fluxo.Services.Notifications;
using Xunit;

namespace Fluxo.Tests.Services.Notifications;

public sealed class FloatingNotificationPublisherTests
{
    [Fact]
    public void SaveFailed_PublishesOneDeduplicatedRequest()
    {
        var messenger = new StrongReferenceMessenger();
        ShowFloatingNotificationMessage? received = null;
        messenger.Register<ShowFloatingNotificationMessage>(this, (_, message) => received = message);

        FloatingNotificationPublisher.SaveFailed(
            messenger,
            ["Amount is required.", "Amount is required.", "Choose an account."]);

        Assert.NotNull(received);
        Assert.Equal("Save failed", received!.Value.Header);
        Assert.Equal(["Amount is required.", "Choose an account."], received.Value.Details);
        Assert.Equal(NotificationSeverity.Warning, received.Value.Severity);
    }

    [Fact]
    public void Success_WithHistoryAction_PublishesClickableRequest()
    {
        var messenger = new StrongReferenceMessenger();
        ShowFloatingNotificationMessage? received = null;
        messenger.Register<ShowFloatingNotificationMessage>(this, (_, message) => received = message);

        FloatingNotificationPublisher.Success(messenger, "Expense added", "Lunch was recorded.", true);

        Assert.NotNull(received!.Value.ClickAsync);
        Assert.Contains("Click to view in History", received.Value.Message);
    }
}
