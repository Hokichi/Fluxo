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

        Assert.All(vm.TrendBarItems, item =>
        {
            Assert.True(item.HideValueText);
            Assert.True(item.RotateLabelVertical);
        });

        vm.SelectedTrendMode = AnalyticsTrendMode.Expenses;
        Assert.All(vm.TrendBarItems, item =>
        {
            Assert.True(item.IsExpenseMode);
            Assert.False(item.IsIncomeMode);
        });

        vm.SelectedTrendMode = AnalyticsTrendMode.Incomes;
        Assert.All(vm.TrendBarItems, item =>
        {
            Assert.False(item.IsExpenseMode);
            Assert.True(item.IsIncomeMode);
        });
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
