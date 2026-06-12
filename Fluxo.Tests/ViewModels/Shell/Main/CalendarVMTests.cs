using Fluxo.Core.DTO;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Services;
using Fluxo.ViewModels.Shell.Main;
using NSubstitute;
using Xunit;

namespace Fluxo.Tests.ViewModels.Shell.Main;

public sealed class CalendarVMTests
{
    [Fact]
    public void Constructor_LoadsCurrentMonthFromFirstVisibleRow()
    {
        var vm = CreateVm(currentDate: new DateTime(2026, 6, 12));

        Assert.Equal(new DateOnly(2026, 6, 12), vm.SelectedDate);
        Assert.Equal("June 2026", vm.VisibleMonthLabel);
        Assert.Equal(6, vm.VisibleWeeks.Count);
        Assert.Equal(new DateOnly(2026, 5, 31), vm.VisibleWeeks[0].Days[0].Date);
        Assert.Equal(new DateOnly(2026, 7, 11), vm.VisibleWeeks[5].Days[6].Date);
        Assert.True(vm.VisibleWeeks[1].Days[5].IsSelected);
        Assert.True(vm.VisibleWeeks[0].Days[0].IsOutsideVisibleMonth);
        Assert.True(vm.VisibleWeeks[1].Days[5].IsCurrentDay);
    }

    [Fact]
    public void ScrollDown_DropsFirstWeekAndAppendsNextWeek()
    {
        var vm = CreateVm(currentDate: new DateTime(2026, 6, 12));

        vm.ScrollCalendarRowsCommand.Execute(1);

        Assert.Equal(new DateOnly(2026, 6, 7), vm.VisibleWeeks[0].Days[0].Date);
        Assert.Equal(new DateOnly(2026, 7, 18), vm.VisibleWeeks[5].Days[6].Date);
    }

    [Fact]
    public void ScrollUp_PrependsPreviousWeekAndDropsLastWeek()
    {
        var vm = CreateVm(currentDate: new DateTime(2026, 6, 12));

        vm.ScrollCalendarRowsCommand.Execute(-1);

        Assert.Equal(new DateOnly(2026, 5, 24), vm.VisibleWeeks[0].Days[0].Date);
        Assert.Equal(new DateOnly(2026, 7, 4), vm.VisibleWeeks[5].Days[6].Date);
    }

    [Fact]
    public async Task NavigateToNextMonth_FillsGridFromFirstWeekOfNextMonth()
    {
        var vm = CreateVm(currentDate: new DateTime(2026, 6, 12));

        await vm.NavigateToNextMonthCommand.ExecuteAsync(null);

        Assert.Equal("July 2026", vm.VisibleMonthLabel);
        Assert.Equal(new DateOnly(2026, 6, 28), vm.VisibleWeeks[0].Days[0].Date);
        Assert.Equal(new DateOnly(2026, 8, 8), vm.VisibleWeeks[5].Days[6].Date);
        Assert.True(vm.VisibleWeeks[0].Days[0].IsOutsideVisibleMonth);
        Assert.False(vm.VisibleWeeks[0].Days[3].IsOutsideVisibleMonth);
    }

    [Fact]
    public async Task NavigateToPreviousMonth_FillsGridFromFirstWeekOfPreviousMonth()
    {
        var vm = CreateVm(currentDate: new DateTime(2026, 6, 12));

        await vm.NavigateToPreviousMonthCommand.ExecuteAsync(null);

        Assert.Equal("May 2026", vm.VisibleMonthLabel);
        Assert.Equal(new DateOnly(2026, 4, 26), vm.VisibleWeeks[0].Days[0].Date);
        Assert.Equal(new DateOnly(2026, 6, 6), vm.VisibleWeeks[5].Days[6].Date);
        Assert.True(vm.VisibleWeeks[0].Days[0].IsOutsideVisibleMonth);
        Assert.False(vm.VisibleWeeks[0].Days[5].IsOutsideVisibleMonth);
    }

    [Fact]
    public async Task NavigateToNextMonth_ScrollsThroughIntermediateWeekStartsBeforeSettlingOnTargetMonth()
    {
        CalendarVM? vm = null;
        var observedWeekStarts = new List<DateOnly>();
        vm = CreateVm(
            currentDate: new DateTime(2026, 6, 12),
            navigationDelayAsync: _ =>
            {
                observedWeekStarts.Add(vm!.VisibleWeeks[0].Days[0].Date);
                return Task.CompletedTask;
            });

        await vm.NavigateToNextMonthCommand.ExecuteAsync(null);

        Assert.Equal(
            [
                new DateOnly(2026, 6, 7),
                new DateOnly(2026, 6, 14),
                new DateOnly(2026, 6, 21)
            ],
            observedWeekStarts);
        Assert.Equal(new DateOnly(2026, 6, 28), vm.VisibleWeeks[0].Days[0].Date);
        Assert.Equal("July 2026", vm.VisibleMonthLabel);
    }

    [Fact]
    public async Task SelectDate_LoadsCalendarDataAndUpdatesSummaryAndEmptyStates()
    {
        var service = Substitute.For<ICalendarService>();
        service.GetCalendarDayAsync(new DateOnly(2026, 6, 12), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CalendarDto(
                new DateOnly(2026, 6, 12),
                92m,
                450m,
                [new CalendarExpenseItem(1, "Groceries", 74m, "Checking", "Food")],
                [new CalendarIncomeItem(2, "Freelance", 450m, "Checking")],
                [],
                [new CalendarRecurringTransactionItem(3, "Rent", 1200m, RecurringTransactionType.Expense, RecurringPeriod.Monthly, 12, "Checking")])));
        var vm = new CalendarVM(service, new DateTime(2026, 6, 1));

        await vm.SelectDateAsync(new DateOnly(2026, 6, 12));

        Assert.Equal(new DateOnly(2026, 6, 12), vm.SelectedDate);
        Assert.Equal("92", vm.TotalSpentText);
        Assert.Equal("450", vm.TotalEarnedText);
        Assert.Equal("0", vm.GoalsDueText);
        Assert.Equal("1", vm.PaymentDueText);
        Assert.False(vm.HasNoExpenses);
        Assert.False(vm.HasNoIncomes);
        Assert.True(vm.HasNoGoalDeadlines);
        Assert.False(vm.HasNoRecurringTransactions);
    }

    [Fact]
    public async Task SelectDate_WhenOlderLoadCompletesAfterNewerLoad_KeepsNewerDetails()
    {
        var olderDate = new DateOnly(2026, 6, 12);
        var newerDate = new DateOnly(2026, 6, 13);
        var olderLoad = new TaskCompletionSource<CalendarDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var newerLoad = new TaskCompletionSource<CalendarDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = Substitute.For<ICalendarService>();
        service.GetCalendarDayAsync(olderDate, Arg.Any<CancellationToken>()).Returns(olderLoad.Task);
        service.GetCalendarDayAsync(newerDate, Arg.Any<CancellationToken>()).Returns(newerLoad.Task);
        var vm = new CalendarVM(service, new DateTime(2026, 6, 1));

        var olderSelection = vm.SelectDateAsync(olderDate);
        var newerSelection = vm.SelectDateAsync(newerDate);
        newerLoad.SetResult(CreateDto(newerDate, totalSpent: 200m));
        await newerSelection;

        olderLoad.SetResult(CreateDto(olderDate, totalSpent: 100m));
        await olderSelection;

        Assert.Equal(newerDate, vm.SelectedDate);
        Assert.Equal("200", vm.TotalSpentText);
    }

    [Fact]
    public async Task SelectDate_WhenNewerLoadCancelsOlderLoad_DoesNotDisposeOlderTokenBeforeItFinishes()
    {
        var olderDate = new DateOnly(2026, 6, 12);
        var newerDate = new DateOnly(2026, 6, 13);
        var inspectOlderToken = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = Substitute.For<ICalendarService>();
        service.GetCalendarDayAsync(olderDate, Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var token = call.Arg<CancellationToken>();
                await inspectOlderToken.Task;
                using var registration = token.Register(() => { });
                return CreateDto(olderDate, totalSpent: 100m);
            });
        service.GetCalendarDayAsync(newerDate, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(CreateDto(newerDate, totalSpent: 200m)));
        var vm = new CalendarVM(service, new DateTime(2026, 6, 1));

        var olderSelection = vm.SelectDateAsync(olderDate);
        var newerSelection = vm.SelectDateAsync(newerDate);
        await newerSelection;
        inspectOlderToken.SetResult();
        await olderSelection;

        Assert.Equal(newerDate, vm.SelectedDate);
        Assert.Equal("200", vm.TotalSpentText);
    }

    [Fact]
    public async Task SelectDate_WhenCurrentLoadIsCanceled_ClearsLoadingState()
    {
        using var cts = new CancellationTokenSource();
        var service = Substitute.For<ICalendarService>();
        service.GetCalendarDayAsync(new DateOnly(2026, 6, 12), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var load = new TaskCompletionSource<CalendarDto>(TaskCreationOptions.RunContinuationsAsynchronously);
                var token = call.Arg<CancellationToken>();
                token.Register(() => load.SetCanceled(token));
                return load.Task;
            });
        var vm = new CalendarVM(service, new DateTime(2026, 6, 1));

        var selection = vm.SelectDateAsync(new DateOnly(2026, 6, 12), cts.Token);
        cts.Cancel();
        await selection;

        Assert.False(vm.IsLoading);
    }

    private static CalendarVM CreateVm(
        DateTime currentDate,
        Func<TimeSpan, Task>? navigationDelayAsync = null)
    {
        var service = Substitute.For<ICalendarService>();
        service.GetCalendarDayAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(new CalendarDto(
                call.Arg<DateOnly>(),
                0m,
                0m,
                [],
                [],
                [],
                [])));

        return new CalendarVM(service, currentDate, navigationDelayAsync);
    }

    private static CalendarDto CreateDto(DateOnly date, decimal totalSpent)
    {
        return new CalendarDto(
            date,
            totalSpent,
            0m,
            [],
            [],
            [],
            []);
    }
}
