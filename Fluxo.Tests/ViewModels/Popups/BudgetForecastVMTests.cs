using Fluxo.Core.Enums;
using Fluxo.Core.Entities;
using Fluxo.ViewModels.Popups;
using Xunit;

namespace Fluxo.Tests.ViewModels.Popups;

public sealed class BudgetForecastVMTests
{
    [Fact]
    public void CountOccurrences_Monthly_CountsEveryDueDateThroughTarget()
    {
        var count = BudgetForecastVM.CountRecurringOccurrences(
            RecurringPeriod.Monthly,
            recurringTime: 1,
            today: new DateTime(2026, 6, 22),
            targetDate: new DateTime(2026, 8, 10));

        Assert.Equal(2, count);
    }

    [Fact]
    public void CountOccurrences_Weekly_CountsMatchingWeekdaysThroughTarget()
    {
        var count = BudgetForecastVM.CountRecurringOccurrences(
            RecurringPeriod.Weekly,
            recurringTime: 5,
            today: new DateTime(2026, 6, 22),
            targetDate: new DateTime(2026, 7, 3));

        Assert.Equal(2, count);
    }

    [Fact]
    public void CalculateDailyAmount_MultipliesByDaysBetweenTodayAndTarget()
    {
        Assert.Equal(300m, BudgetForecastVM.CalculateDailyProjection(100m, 3));
    }

    [Theory]
    [InlineData(true, 300)]
    [InlineData(false, 100)]
    public void CalculateEventAmount_OnlyDailyEventsMultiplyByDayCount(bool isDaily, decimal expected)
    {
        Assert.Equal(expected, BudgetForecastVM.CalculateEventAmount(100m, dayCount: 3, isDaily));
    }

    [Fact]
    public void CountAllocationPeriods_RoundsUp()
    {
        Assert.Equal(2, BudgetForecastVM.CountAllocationPeriods(dayCount: 12, allocationPeriodDays: 7));
    }

    [Fact]
    public void CalculateCategoryBudget_MultipliesBaseAllocationByRoundedPeriodCount()
    {
        Assert.Equal(2_000m, BudgetForecastVM.CalculateCategoryBudget(1_000m, dayCount: 12, allocationPeriodDays: 7));
    }

    [Fact]
    public void CalculateInstallmentValidationAmount_AdjustsForAllocationPeriodWeeks()
    {
        Assert.Equal(50m, BudgetForecastVM.CalculateInstallmentValidationAmount(400m, installmentCount: 2m, allocationPeriodWeeks: 1m));
    }

    [Fact]
    public void GetAvailableBalance_ForCredit_ReturnsLimitMinusSpent()
    {
        var account = new Account
        {
            AccountType = AccountType.Credit,
            AccountLimit = 10_000m,
            SpentAmount = 3_250m,
            Balance = 0m
        };

        Assert.Equal(6_750m, BudgetForecastVM.GetAvailableBalance(account));
    }

    [Theory]
    [InlineData(100, 500, "Affordable")]
    [InlineData(600, 1000, "Not recommended")]
    [InlineData(1200, 1000, "Not affordable")]
    public void BuildPurchaseResult_ReturnsExpectedSeverity(decimal purchase, decimal accountRemaining, string expectedStart)
    {
        var result = BudgetForecastVM.BuildPurchaseResult(
            purchaseAmount: purchase,
            accountBalance: accountRemaining,
            categoryRemaining: 500m,
            categoryName: "Needs",
            accountName: "Cash");

        Assert.StartsWith(expectedStart, result.Message, StringComparison.Ordinal);
    }
}
