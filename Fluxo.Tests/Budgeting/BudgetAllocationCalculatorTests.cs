using Fluxo.Core.Budgeting;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Xunit;

namespace Fluxo.Tests.Budgeting;

public class BudgetAllocationCalculatorTests
{
    [Fact]
    public void ResolveCurrentPeriod_MonthlyForFebruary2026_ReturnsFullMonth()
    {
        var period = BudgetAllocationCalculator.ResolveCurrentPeriod(
            AllocationPeriod.Monthly,
            new DateTime(2026, 2, 10));

        Assert.Equal(new DateTime(2026, 2, 1), period.Start);
        Assert.Equal(new DateTime(2026, 2, 28), period.End);
        Assert.Equal(28, period.DayCount);
    }

    [Fact]
    public void CalculateDailyAllowance_QuarterlyAllocationLimit_ReturnsRoundedDailyAmount()
    {
        var allocation = new BudgetAllocation
        {
            AllocationLimit = 910m,
            AllocationPeriod = AllocationPeriod.Quarterly
        };

        var allowance = BudgetAllocationCalculator.CalculateDailyAllowance(
            allocation,
            new DateTime(2026, 4, 2));

        Assert.Equal(10m, allowance);
    }

    [Fact]
    public void CalculateSnapshot_PooledRollover_DistributesPositivePreviousRemainingByThresholds()
    {
        var allocation = new BudgetAllocation
        {
            AllocationLimit = 1000m,
            NeedsThreshold = 50,
            WantsThreshold = 30,
            InvestThreshold = 20,
            RolloverPolicy = RolloverPolicy.Pooled
        };
        Dictionary<ExpenseCategory, decimal> previousSpent = new()
        {
            [ExpenseCategory.Needs] = 400m,
            [ExpenseCategory.Wants] = 250m,
            [ExpenseCategory.Savings] = 180m
        };

        var snapshot = BudgetAllocationCalculator.CalculateSnapshot(
            allocation,
            new Dictionary<ExpenseCategory, decimal>(),
            previousSpent,
            new DateTime(2026, 2, 10));

        Assert.Equal(585m, snapshot.Needs.Available);
        Assert.Equal(351m, snapshot.Wants.Available);
        Assert.Equal(234m, snapshot.Invest.Available);
    }

    [Fact]
    public void CalculateSoftDebtDelta_WhenRemainingIsAlreadyNegative_ReturnsFullExpenseAmount()
    {
        var delta = BudgetAllocationCalculator.CalculateSoftDebtDelta(-20m, 15m);

        Assert.Equal(15m, delta);
    }

    [Fact]
    public void ResolveCurrentPeriod_WeeklyOnMonday_StartsThatMondayAndEndsSunday()
    {
        var period = BudgetAllocationCalculator.ResolveCurrentPeriod(
            AllocationPeriod.Weekly,
            new DateTime(2026, 6, 1));

        Assert.Equal(new DateTime(2026, 6, 1), period.Start);
        Assert.Equal(new DateTime(2026, 6, 7), period.End);
        Assert.Equal(7, period.DayCount);
    }

    [Fact]
    public void ResolveCurrentPeriod_BiweeklyUsesContinuousFixedMondayAnchor()
    {
        var period = BudgetAllocationCalculator.ResolveCurrentPeriod(
            AllocationPeriod.Biweekly,
            new DateTime(2026, 1, 18));

        Assert.Equal(new DateTime(2026, 1, 12), period.Start);
        Assert.Equal(new DateTime(2026, 1, 25), period.End);
        Assert.Equal(14, period.DayCount);
    }

    [Fact]
    public void ResolveCurrentPeriod_BiweeklyAcrossYearBoundary_UsesSameContinuousPeriod()
    {
        var decemberPeriod = BudgetAllocationCalculator.ResolveCurrentPeriod(
            AllocationPeriod.Biweekly,
            new DateTime(2025, 12, 29));
        var januaryPeriod = BudgetAllocationCalculator.ResolveCurrentPeriod(
            AllocationPeriod.Biweekly,
            new DateTime(2026, 1, 4));
        var laterJanuaryPeriod = BudgetAllocationCalculator.ResolveCurrentPeriod(
            AllocationPeriod.Biweekly,
            new DateTime(2026, 1, 11));

        Assert.Equal(decemberPeriod, januaryPeriod);
        Assert.Equal(decemberPeriod, laterJanuaryPeriod);
        Assert.Equal(new DateTime(2025, 12, 29), decemberPeriod.Start);
        Assert.Equal(new DateTime(2026, 1, 11), decemberPeriod.End);
        Assert.Equal(14, decemberPeriod.DayCount);
    }

    [Fact]
    public void ResolveCurrentPeriod_BiweeklyAdjacentYearBoundaryPeriods_DoNotOverlap()
    {
        var priorPeriod = BudgetAllocationCalculator.ResolveCurrentPeriod(
            AllocationPeriod.Biweekly,
            new DateTime(2025, 12, 28));
        var nextPeriod = BudgetAllocationCalculator.ResolveCurrentPeriod(
            AllocationPeriod.Biweekly,
            new DateTime(2025, 12, 29));

        Assert.Equal(new DateTime(2025, 12, 15), priorPeriod.Start);
        Assert.Equal(new DateTime(2025, 12, 28), priorPeriod.End);
        Assert.Equal(new DateTime(2025, 12, 29), nextPeriod.Start);
        Assert.Equal(new DateTime(2026, 1, 11), nextPeriod.End);
        Assert.True(priorPeriod.End < nextPeriod.Start);
    }

    [Fact]
    public void ResolvePreviousPeriod_Biweekly_ReturnsImmediatelyPreviousContinuousPeriod()
    {
        var period = BudgetAllocationCalculator.ResolvePreviousPeriod(
            AllocationPeriod.Biweekly,
            new DateTime(2026, 1, 1));

        Assert.Equal(new DateTime(2025, 12, 15), period.Start);
        Assert.Equal(new DateTime(2025, 12, 28), period.End);
        Assert.Equal(14, period.DayCount);
    }

    [Fact]
    public void CalculateSnapshot_MatchingRollover_RollsEachPositivePreviousRemainingToSameSegment()
    {
        var allocation = new BudgetAllocation
        {
            AllocationLimit = 1000m,
            NeedsThreshold = 50,
            WantsThreshold = 30,
            InvestThreshold = 20,
            RolloverPolicy = RolloverPolicy.Matching
        };
        Dictionary<ExpenseCategory, decimal> previousSpent = new()
        {
            [ExpenseCategory.Needs] = 450m,
            [ExpenseCategory.Wants] = 350m,
            [ExpenseCategory.Savings] = 50m
        };

        var snapshot = BudgetAllocationCalculator.CalculateSnapshot(
            allocation,
            new Dictionary<ExpenseCategory, decimal>(),
            previousSpent,
            new DateTime(2026, 2, 10));

        Assert.Equal(550m, snapshot.Needs.Available);
        Assert.Equal(300m, snapshot.Wants.Available);
        Assert.Equal(350m, snapshot.Invest.Available);
    }

    [Fact]
    public void CalculateSnapshot_CategoryDebt_ReducesAvailability()
    {
        var allocation = new BudgetAllocation
        {
            AllocationLimit = 1000m,
            NeedsThreshold = 50,
            WantsThreshold = 30,
            InvestThreshold = 20,
            NeedsDebt = 40m,
            WantsDebt = 25m,
            InvestDebt = 10m
        };

        var snapshot = BudgetAllocationCalculator.CalculateSnapshot(
            allocation,
            new Dictionary<ExpenseCategory, decimal>(),
            new Dictionary<ExpenseCategory, decimal>(),
            new DateTime(2026, 2, 10));

        Assert.Equal(460m, snapshot.Needs.Available);
        Assert.Equal(275m, snapshot.Wants.Available);
        Assert.Equal(190m, snapshot.Invest.Available);
    }

    [Fact]
    public void CalculateSnapshot_CurrentSpent_UpdatesRemainingAndPercentage()
    {
        var allocation = new BudgetAllocation
        {
            AllocationLimit = 1000m,
            NeedsThreshold = 50,
            WantsThreshold = 30,
            InvestThreshold = 20
        };
        Dictionary<ExpenseCategory, decimal> currentSpent = new()
        {
            [ExpenseCategory.Needs] = 125m,
            [ExpenseCategory.Wants] = 75m,
            [ExpenseCategory.Savings] = 40m
        };

        var snapshot = BudgetAllocationCalculator.CalculateSnapshot(
            allocation,
            currentSpent,
            new Dictionary<ExpenseCategory, decimal>(),
            new DateTime(2026, 2, 10));

        Assert.Equal(375m, snapshot.Needs.Remaining);
        Assert.Equal(25, snapshot.Needs.Percentage);
        Assert.Equal(225m, snapshot.Wants.Remaining);
        Assert.Equal(25, snapshot.Wants.Percentage);
        Assert.Equal(160m, snapshot.Invest.Remaining);
        Assert.Equal(20, snapshot.Invest.Percentage);
    }

    [Fact]
    public void WouldHardStop_ReturnsTrueOnlyWhenExpenseExceedsRemainingBudget()
    {
        var category = new BudgetAllocationCategoryState(
            BudgetAllocationSegment.Needs,
            BaseAllocation: 500m,
            Rollover: 0m,
            Debt: 0m,
            Spent: 475m,
            Available: 500m,
            Remaining: 25m,
            Percentage: 95);

        Assert.False(BudgetAllocationCalculator.WouldHardStop(category, 25m));
        Assert.True(BudgetAllocationCalculator.WouldHardStop(category, 25.01m));
    }
}
