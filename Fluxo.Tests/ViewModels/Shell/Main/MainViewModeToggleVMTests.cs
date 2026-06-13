using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Enums;
using Fluxo.Resources.Resources.Messages;
using Fluxo.Services.Dialogs;
using Fluxo.Services.Ui;
using Fluxo.ViewModels.Shell;
using Fluxo.ViewModels.Shell.Main;
using NSubstitute;
using System.Windows;
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

    [Fact]
    public async Task MoveToCurrentPeriodFromUserAsync_UsesToastAndWaitsForUiReady()
    {
        var messenger = new WeakReferenceMessenger();
        var dialogService = Substitute.For<IDialogService>();
        var uiSettleAwaiter = Substitute.For<IUiSettleAwaiter>();
        var toastDelegateInvoked = false;
        dialogService.ShowToastWhileAsync(
                Arg.Any<string>(),
                Arg.Any<Func<Task>>(),
                Arg.Any<Window?>())
            .Returns(async callInfo =>
            {
                toastDelegateInvoked = true;
                await ((Func<Task>)callInfo[1])();
            });
        var recipient = new MessageCaptureRecipient();
        messenger.Register<MessageCaptureRecipient, MoveToCurrentPeriodRequestedMessage>(
            recipient,
            static (target, message) => target.MoveRequests.Add(message));
        var vm = new MainViewModeToggleVM(messenger, dialogService, uiSettleAwaiter);

        await vm.MoveToCurrentPeriodFromUserAsync();

        await dialogService.Received(1).ShowToastWhileAsync(
            Arg.Is<string>(message => message.Contains("Loading", StringComparison.Ordinal)),
            Arg.Any<Func<Task>>(),
            Arg.Any<Window?>());
        await uiSettleAwaiter.Received(1).WaitForUiReadyAsync(
            Arg.Any<Window?>(),
            Arg.Any<CancellationToken>());
        Assert.True(toastDelegateInvoked);
        Assert.Single(recipient.MoveRequests);
    }

    private sealed class MessageCaptureRecipient
    {
        public List<ViewModeChangeMessage> Messages { get; } = [];

        public List<MoveToCurrentPeriodRequestedMessage> MoveRequests { get; } = [];
    }
}
