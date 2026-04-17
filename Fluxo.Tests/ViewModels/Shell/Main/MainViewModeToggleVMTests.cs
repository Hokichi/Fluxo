using CommunityToolkit.Mvvm.Messaging;
using Fluxo.ViewModels.Messages;
using Fluxo.ViewModels.Shell;
using Xunit;

namespace Fluxo.Tests.ViewModels.Shell.Main;

public class MainViewModeToggleVMTests
{
    [Fact]
    public void SetSelectedMainContentViewCommand_PublishesViewModeChangeMessage()
    {
        var messenger = new WeakReferenceMessenger();
        var recipient = new MessageCaptureRecipient();
        messenger.Register<MessageCaptureRecipient, ViewModeChangeMessage>(
            recipient,
            static (target, message) => target.Messages.Add(message));

        var vm = new MainViewModeToggleVM(messenger);

        vm.SetSelectedMainContentViewCommand.Execute(MainContentViewMode.Weekly);

        Assert.Single(recipient.Messages);
        Assert.Equal(MainContentViewMode.Weekly, recipient.Messages[0].Value);
    }

    [Fact]
    public void SetSelectedMainContentViewCommand_UpdatesSelectionFlags()
    {
        var vm = new MainViewModeToggleVM();

        vm.SetSelectedMainContentViewCommand.Execute(MainContentViewMode.Monthly);

        Assert.Equal(MainContentViewMode.Monthly, vm.SelectedMainContentViewMode);
        Assert.False(vm.IsDailyViewSelected);
        Assert.False(vm.IsWeeklyViewSelected);
        Assert.True(vm.IsMonthlyViewSelected);
        Assert.False(vm.IsAllTimeViewSelected);
    }

    [Fact]
    public void MoveToCurrentPeriodCommand_PublishesMoveRequestMessage()
    {
        var messenger = new WeakReferenceMessenger();
        var recipient = new MessageCaptureRecipient();
        messenger.Register<MessageCaptureRecipient, MoveToCurrentPeriodRequestedMessage>(
            recipient,
            static (target, message) => target.MoveRequests.Add(message));
        var vm = new MainViewModeToggleVM(messenger);

        vm.MoveToCurrentPeriodCommand.Execute(null);

        Assert.Single(recipient.MoveRequests);
    }

    [Fact]
    public void SpinnerPeriodStateChangedMessage_UpdatesMoveToCurrentUiState()
    {
        var messenger = new WeakReferenceMessenger();
        var vm = new MainViewModeToggleVM(messenger);

        messenger.Send(new SpinnerPeriodStateChangedMessage(new SpinnerPeriodState(
            IsAtCurrentPeriod: false,
            IsSpinnerVisible: true,
            MoveToCurrentLabel: "Move to current week")));

        Assert.False(vm.IsAtCurrentPeriod);
        Assert.True(vm.IsSpinnerVisible);
        Assert.Equal("Move to current week", vm.MoveToCurrentLabel);
    }

    private sealed class MessageCaptureRecipient
    {
        public List<ViewModeChangeMessage> Messages { get; } = [];

        public List<MoveToCurrentPeriodRequestedMessage> MoveRequests { get; } = [];
    }
}
