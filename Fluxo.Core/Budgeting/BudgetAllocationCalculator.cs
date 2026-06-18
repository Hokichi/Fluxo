using Fluxo.Core.Entities;
using Fluxo.Core.Enums;

namespace Fluxo.Core.Budgeting;

public static class BudgetAllocationCalculator
{
    public static BudgetAllocationPeriod ResolveCurrentPeriod(
        AllocationPeriod period,
        DateTime today,
        int periodStart = 1)
    {
        return BudgetAllocationPeriodRules.ResolveCurrentPeriod(period, today, periodStart);
    }

    public static BudgetAllocationPeriod ResolvePreviousPeriod(
        AllocationPeriod period,
        DateTime today,
        int periodStart = 1)
    {
        return BudgetAllocationPeriodRules.ResolvePreviousPeriod(period, today, periodStart);
    }

    public static decimal CalculateDailyAllowance(
        BudgetAllocation allocation,
        DateTime today,
        decimal fallbackBudgetBase = 0m)
    {
        var period = ResolveCurrentPeriod(allocation.AllocationPeriod, today, allocation.PeriodStart);

        if (period.DayCount <= 0)
            return 0m;

        return RoundMoney(ResolveBudgetBase(allocation, fallbackBudgetBase) / period.DayCount);
    }

    public static BudgetAllocationSnapshot CalculateSnapshot(
        BudgetAllocation allocation,
        IReadOnlyDictionary<ExpenseCategory, decimal> currentPeriodSpentByCategory,
        IReadOnlyDictionary<ExpenseCategory, decimal> previousPeriodSpentByCategory,
        DateTime today,
        decimal fallbackBudgetBase = 0m)
    {
        var currentPeriod = ResolveCurrentPeriod(allocation.AllocationPeriod, today, allocation.PeriodStart);
        var previousPeriod = ResolvePreviousPeriod(allocation.AllocationPeriod, today, allocation.PeriodStart);
        var budgetBase = ResolveBudgetBase(allocation, fallbackBudgetBase);

        var needsBase = CalculateBaseAllocation(budgetBase, allocation.NeedsThreshold);
        var wantsBase = CalculateBaseAllocation(budgetBase, allocation.WantsThreshold);
        var investBase = CalculateBaseAllocation(budgetBase, allocation.InvestThreshold);

        var rolloverPolicy = ShouldApplyRollover(allocation, currentPeriod)
            ? allocation.RolloverPolicy
            : RolloverPolicy.None;

        var rollovers = CalculateRollovers(
            rolloverPolicy,
            previousPeriodSpentByCategory,
            needsBase,
            wantsBase,
            investBase,
            allocation.NeedsThreshold,
            allocation.WantsThreshold,
            allocation.InvestThreshold);

        var needs = CreateCategoryState(
            BudgetAllocationSegment.Needs,
            needsBase,
            rollovers.Needs,
            allocation.NeedsDebt,
            GetSpent(currentPeriodSpentByCategory, ExpenseCategory.Needs));
        var wants = CreateCategoryState(
            BudgetAllocationSegment.Wants,
            wantsBase,
            rollovers.Wants,
            allocation.WantsDebt,
            GetSpent(currentPeriodSpentByCategory, ExpenseCategory.Wants));
        var invest = CreateCategoryState(
            BudgetAllocationSegment.Invest,
            investBase,
            rollovers.Invest,
            allocation.InvestDebt,
            GetSpent(currentPeriodSpentByCategory, ExpenseCategory.Savings));

        return new BudgetAllocationSnapshot(
            currentPeriod,
            previousPeriod,
            budgetBase,
            CalculateDailyAllowance(allocation, today, fallbackBudgetBase),
            needs,
            wants,
            invest);
    }

    public static bool WouldHardStop(BudgetAllocationCategoryState category, decimal expenseAmount)
    {
        return expenseAmount > category.Remaining;
    }

    public static decimal CalculateSoftDebtDelta(decimal remainingBudget, decimal expenseAmount)
    {
        return Math.Max(0m, expenseAmount - Math.Max(0m, remainingBudget));
    }

    private static decimal ResolveBudgetBase(BudgetAllocation allocation, decimal fallbackBudgetBase)
    {
        return allocation.AllocationLimit > 0m ? allocation.AllocationLimit : fallbackBudgetBase;
    }

    private static decimal CalculateBaseAllocation(decimal budgetBase, int threshold)
    {
        return RoundMoney(budgetBase * threshold / 100m);
    }

    private static (decimal Needs, decimal Wants, decimal Invest) CalculateRollovers(
        RolloverPolicy rolloverPolicy,
        IReadOnlyDictionary<ExpenseCategory, decimal> previousPeriodSpentByCategory,
        decimal needsBase,
        decimal wantsBase,
        decimal investBase,
        int needsThreshold,
        int wantsThreshold,
        int investThreshold)
    {
        if (rolloverPolicy == RolloverPolicy.None)
            return (0m, 0m, 0m);

        var needsRemaining = Math.Max(0m, needsBase - GetSpent(previousPeriodSpentByCategory, ExpenseCategory.Needs));
        var wantsRemaining = Math.Max(0m, wantsBase - GetSpent(previousPeriodSpentByCategory, ExpenseCategory.Wants));
        var investRemaining = Math.Max(0m, investBase - GetSpent(previousPeriodSpentByCategory, ExpenseCategory.Savings));

        if (rolloverPolicy == RolloverPolicy.Matching)
            return (needsRemaining, wantsRemaining, investRemaining);

        var pool = needsRemaining + wantsRemaining + investRemaining;

        return (
            CalculateBaseAllocation(pool, needsThreshold),
            CalculateBaseAllocation(pool, wantsThreshold),
            CalculateBaseAllocation(pool, investThreshold));
    }

    private static bool ShouldApplyRollover(BudgetAllocation allocation, BudgetAllocationPeriod currentPeriod)
    {
        return allocation.RolloverPolicy != RolloverPolicy.None &&
               allocation.LastRolloverPeriodStart.Date < currentPeriod.Start;
    }

    private static BudgetAllocationCategoryState CreateCategoryState(
        BudgetAllocationSegment segment,
        decimal baseAllocation,
        decimal rollover,
        decimal debt,
        decimal spent)
    {
        var available = baseAllocation + rollover - debt;
        var remaining = available - spent;
        var percentage = available <= 0m
            ? 0
            : (int)Math.Round(spent / available * 100m, 0, MidpointRounding.AwayFromZero);

        return new BudgetAllocationCategoryState(
            segment,
            baseAllocation,
            rollover,
            debt,
            spent,
            available,
            remaining,
            percentage);
    }

    private static decimal GetSpent(
        IReadOnlyDictionary<ExpenseCategory, decimal> spentByCategory,
        ExpenseCategory category)
    {
        return spentByCategory.TryGetValue(category, out var spent) ? spent : 0m;
    }

    private static decimal RoundMoney(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
