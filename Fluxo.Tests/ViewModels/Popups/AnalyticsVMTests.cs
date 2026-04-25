using System;
using System.Threading;
using System.Threading.Tasks;
using Fluxo.Core.DTO;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Services;
using Fluxo.ViewModels.Popups;
using Fluxo.ViewModels.Shell.Main;
using NSubstitute;
using Xunit;
using AnalyticsVM = Fluxo.ViewModels.Shell.Main.AnalyticsVM;

namespace Fluxo.Tests.ViewModels.Popups;

public sealed class AnalyticsVMTests
{
    [Fact]
    public void DateRangeOver31Days_AdjustsEndDateAndShowsWarning()
    {
        var vm = CreateVm();

        vm.StartDate = new DateTime(2026, 1, 1);
        vm.EndDate = new DateTime(2026, 2, 20);

        Assert.Equal(new DateTime(2026, 2, 1), vm.EndDate.Date);
        Assert.True(vm.HasDateRangeWarning);
        Assert.Contains("31 days", vm.DateRangeWarningMessage);
    }

    [Fact]
    public void DateRangeOver14Days_RotatesLabelsAndHidesTrendValues()
    {
        var vm = CreateVm();

        vm.StartDate = new DateTime(2026, 1, 1);
        vm.EndDate = new DateTime(2026, 1, 20);
        Assert.True(vm.IsTrendLabelVertical);
        Assert.True(vm.HideTrendValueLabels);

        vm.EndDate = new DateTime(2026, 1, 10);
        Assert.False(vm.IsTrendLabelVertical);
        Assert.False(vm.HideTrendValueLabels);
    }

    [Fact]
    public async Task TrendBarsUseModeSpecificColorFlags()
    {
        var vm = CreateVm();
        vm.StartDate = new DateTime(2026, 1, 1);
        vm.EndDate = new DateTime(2026, 1, 20);
        await vm.LoadAsync();

        Assert.Equal(3, vm.TrendBarItems.Count);
        Assert.All(vm.TrendBarItems, item =>
        {
            Assert.True(item.HideValueText);
            Assert.True(item.RotateLabelVertical);
        });

        Assert.Equal("Thu", vm.TrendBarItems[0].Label);
        Assert.Equal(40m, vm.TrendBarItems[0].Value);
        Assert.Equal(100m, vm.TrendBarItems[0].SecondaryValue);
        Assert.True(vm.TrendBarItems[0].HasSecondaryBar);
        Assert.True(vm.TrendBarItems[0].IsExpenseMode);
        Assert.False(vm.TrendBarItems[0].IsIncomeMode);
        Assert.False(vm.TrendBarItems[0].IsSecondaryExpenseMode);
        Assert.True(vm.TrendBarItems[0].IsSecondaryIncomeMode);

        Assert.Equal("Fri", vm.TrendBarItems[1].Label);
        Assert.Equal(60m, vm.TrendBarItems[1].Value);
        Assert.Equal(160m, vm.TrendBarItems[1].SecondaryValue);
        Assert.True(vm.TrendBarItems[1].HasSecondaryBar);
        Assert.True(vm.TrendBarItems[1].IsExpenseMode);
        Assert.False(vm.TrendBarItems[1].IsIncomeMode);
        Assert.False(vm.TrendBarItems[1].IsSecondaryExpenseMode);
        Assert.True(vm.TrendBarItems[1].IsSecondaryIncomeMode);

        Assert.Equal("Sat", vm.TrendBarItems[2].Label);
        Assert.Equal(55m, vm.TrendBarItems[2].Value);
        Assert.Equal(120m, vm.TrendBarItems[2].SecondaryValue);
        Assert.True(vm.TrendBarItems[2].HasSecondaryBar);
        Assert.True(vm.TrendBarItems[2].IsExpenseMode);
        Assert.False(vm.TrendBarItems[2].IsIncomeMode);
        Assert.False(vm.TrendBarItems[2].IsSecondaryExpenseMode);
        Assert.True(vm.TrendBarItems[2].IsSecondaryIncomeMode);

        vm.SelectedTrendMode = AnalyticsTrendMode.Expenses;
        Assert.Equal(3, vm.TrendBarItems.Count);
        Assert.All(vm.TrendBarItems, item =>
        {
            Assert.False(item.HasSecondaryBar);
            Assert.Equal(0m, item.SecondaryValue);
            Assert.True(item.IsExpenseMode);
            Assert.False(item.IsIncomeMode);
            Assert.False(item.IsSecondaryExpenseMode);
            Assert.False(item.IsSecondaryIncomeMode);
        });

        vm.SelectedTrendMode = AnalyticsTrendMode.Incomes;
        Assert.Equal(3, vm.TrendBarItems.Count);
        Assert.All(vm.TrendBarItems, item =>
        {
            Assert.False(item.HasSecondaryBar);
            Assert.Equal(0m, item.SecondaryValue);
            Assert.False(item.IsExpenseMode);
            Assert.True(item.IsIncomeMode);
            Assert.False(item.IsSecondaryExpenseMode);
            Assert.False(item.IsSecondaryIncomeMode);
        });
    }

    [Fact]
    public async Task ApplyExternalDateRangeWithoutRefresh_UsesRangeOnFirstLoad()
    {
        var service = Substitute.For<IAnalyticsService>();
        service.GetAnalyticsAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AnalyticsDto(
                TotalIncome: 0m,
                TotalExpense: 0m,
                TimeSeries: [],
                CategoryRatio: [],
                TopSpendingTags: [],
                GoalsCreatedInPeriod: [])));
        var vm = new AnalyticsVM(service);

        vm.ApplyExternalDateRange(
            new DateTime(2026, 1, 6),
            new DateTime(2026, 1, 12),
            refresh: false);

        await vm.LoadAsync();

        await service.Received(1).GetAnalyticsAsync(
            new DateOnly(2026, 1, 6),
            new DateOnly(2026, 1, 12),
            Arg.Any<CancellationToken>());
    }

    private static AnalyticsVM CreateVm()
    {
        var service = Substitute.For<IAnalyticsService>();
        service.GetAnalyticsAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AnalyticsDto(
                TotalIncome: 1000m,
                TotalExpense: 500m,
                TimeSeries:
                [
                    new AnalyticsTimeSeriesPoint(new DateOnly(2026, 1, 1), 100m, 40m),
                    new AnalyticsTimeSeriesPoint(new DateOnly(2026, 1, 2), 160m, 60m),
                    new AnalyticsTimeSeriesPoint(new DateOnly(2026, 1, 3), 120m, 55m)
                ],
                CategoryRatio:
                [
                    new AnalyticsCategorySlice(ExpenseCategory.Needs, 200m),
                    new AnalyticsCategorySlice(ExpenseCategory.Wants, 150m),
                    new AnalyticsCategorySlice(ExpenseCategory.Savings, 100m)
                ],
                TopSpendingTags:
                [
                    new AnalyticsTagTotal("Food", "#FFB86C", 100m)
                ],
                GoalsCreatedInPeriod: [])));

        return new AnalyticsVM(service);
    }
}
