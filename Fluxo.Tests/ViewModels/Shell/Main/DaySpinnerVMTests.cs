using CommunityToolkit.Mvvm.Messaging;
using System.Reflection;
using Fluxo.ViewModels.Controls;
using Fluxo.ViewModels.Messages;
using Fluxo.ViewModels.Shell;
using Xunit;

namespace Fluxo.Tests.ViewModels.Shell.Main;

public class DaySpinnerVMTests
{
    [Fact]
    public void SelectedDay_InDailyMode_PublishesExpectedRange()
    {
        var messenger = new WeakReferenceMessenger();
        var recipient = new MessageCaptureRecipient();
        messenger.Register<MessageCaptureRecipient, DateRangeSelectionChangedMessage>(
            recipient,
            static (target, message) => target.DateRanges.Add(message.Value));

        var vm = new DaySpinnerVM(messenger);

        messenger.Send(new ViewModeChangeMessage(MainContentViewMode.Daily));
        recipient.DateRanges.Clear();

        var selectedDay = vm.DaysOfWeek.First(day => !ReferenceEquals(day, vm.SelectedDay));

        vm.SelectedDay = selectedDay;

        var expected = DateRangeResolver.Resolve(selectedDay.Date, MainContentViewMode.Daily);

        Assert.Single(recipient.DateRanges);
        Assert.Equal(expected.From, recipient.DateRanges[0].From);
        Assert.Equal(expected.To, recipient.DateRanges[0].To);
    }

    [Fact]
    public void AllTimeMode_PublishesAllTimeViewModeMessage()
    {
        var messenger = new WeakReferenceMessenger();
        var recipient = new MessageCaptureRecipient();
        messenger.Register<MessageCaptureRecipient, AllTimeViewModeMessage>(
            recipient,
            static (target, message) => target.AllTimeMessages.Add(message));

        var vm = new DaySpinnerVM(messenger);

        messenger.Send(new ViewModeChangeMessage(MainContentViewMode.AllTime));

        Assert.False(vm.IsSpinnerVisible);
        Assert.Single(recipient.AllTimeMessages);
    }

    [Fact]
    public void WeeklyMode_WhenSelectedSunday_PublishesMondayToSundayRange()
    {
        var messenger = new WeakReferenceMessenger();
        var recipient = new MessageCaptureRecipient();
        messenger.Register<MessageCaptureRecipient, DateRangeSelectionChangedMessage>(
            recipient,
            static (target, message) => target.DateRanges.Add(message.Value));

        var vm = new DaySpinnerVM(messenger);

        messenger.Send(new ViewModeChangeMessage(MainContentViewMode.Weekly));
        recipient.DateRanges.Clear();

        var selectedSunday = new DayOfWeekVM
        {
            Date = new DateTime(2026, 4, 19, 8, 0, 0, DateTimeKind.Local),
            DayName = "Sun",
            DayNumber = "19",
            IsSelected = true
        };

        vm.SelectedDay = selectedSunday;

        Assert.Single(recipient.DateRanges);
        Assert.Equal(new DateTime(2026, 4, 13, 0, 0, 0, DateTimeKind.Local), recipient.DateRanges[0].From);
        Assert.Equal(new DateTime(2026, 4, 19, 0, 0, 0, DateTimeKind.Local), recipient.DateRanges[0].To);
    }

    [Fact]
    public void ComputeWeeklyPageOffset_AcrossIsoYearBoundary_UsesAbsoluteMondayWindows()
    {
        var today = new DateTime(2021, 1, 4, 0, 0, 0, DateTimeKind.Unspecified);
        var previousIsoWeekMonday = new DateTime(2020, 12, 28, 0, 0, 0, DateTimeKind.Unspecified);

        var result = InvokeWeeklyPageOffset(today, previousIsoWeekMonday);

        Assert.Equal(-1, result);
    }

    private static int InvokeWeeklyPageOffset(DateTime today, DateTime referenceDate)
    {
        var method = typeof(DaySpinnerVM).GetMethod(
            "ComputeWeeklyPageOffset",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);

        return (int)method!.Invoke(null, [today, referenceDate])!;
    }

    private sealed class MessageCaptureRecipient
    {
        public List<(DateTime From, DateTime To)> DateRanges { get; } = [];

        public List<AllTimeViewModeMessage> AllTimeMessages { get; } = [];
    }
}