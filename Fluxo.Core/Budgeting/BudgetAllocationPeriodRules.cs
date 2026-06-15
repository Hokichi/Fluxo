using Fluxo.Core.Enums;

namespace Fluxo.Core.Budgeting;

public static class BudgetAllocationPeriodRules
{
    private static readonly DateTime BiweeklyAnchor = new(2001, 1, 1);

    public static int ClampPeriodStart(AllocationPeriod period, int periodStart)
    {
        var upperLimit = period switch
        {
            AllocationPeriod.Weekly => 7,
            AllocationPeriod.Biweekly => 7,
            AllocationPeriod.Monthly => 28,
            AllocationPeriod.Quarterly => 3,
            AllocationPeriod.Yearly => 12,
            _ => 28
        };

        return Math.Clamp(periodStart, 1, upperLimit);
    }

    public static int ResolveCurrentPeriodIndex(AllocationPeriod period, DateTime today)
    {
        var date = today.Date;

        return period switch
        {
            AllocationPeriod.Weekly => ResolveMondayBasedDayOfWeek(date),
            AllocationPeriod.Biweekly => ResolveBiweeklyIndex(date),
            AllocationPeriod.Monthly => Math.Min(date.Day, 28),
            AllocationPeriod.Quarterly => ((date.Month - 1) % 3) + 1,
            AllocationPeriod.Yearly => date.Month,
            _ => Math.Min(date.Day, 28)
        };
    }

    public static BudgetAllocationPeriod ResolveCurrentPeriod(
        AllocationPeriod period,
        DateTime today,
        int periodStart)
    {
        var date = today.Date;
        var clampedStart = ClampPeriodStart(period, periodStart);

        return period switch
        {
            AllocationPeriod.Weekly => ResolveWeeklyPeriod(date, clampedStart),
            AllocationPeriod.Biweekly => ResolveBiweeklyPeriod(date, clampedStart),
            AllocationPeriod.Monthly => ResolveMonthlyPeriod(date, clampedStart),
            AllocationPeriod.Quarterly => ResolveQuarterlyPeriod(date, clampedStart),
            AllocationPeriod.Yearly => ResolveYearlyPeriod(date, clampedStart),
            _ => ResolveMonthlyPeriod(date, clampedStart)
        };
    }

    public static BudgetAllocationPeriod ResolvePreviousPeriod(
        AllocationPeriod period,
        DateTime today,
        int periodStart)
    {
        var current = ResolveCurrentPeriod(period, today, periodStart);
        return ResolveCurrentPeriod(period, current.Start.AddDays(-1), periodStart);
    }

    private static int ResolveMondayBasedDayOfWeek(DateTime date)
    {
        return ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7 + 1;
    }

    private static int ResolveBiweeklyIndex(DateTime date)
    {
        var periodIndex = (int)Math.Floor((date - BiweeklyAnchor).TotalDays / 14d);
        var start = BiweeklyAnchor.AddDays(periodIndex * 14);
        return (int)(date - start).TotalDays + 1;
    }

    private static BudgetAllocationPeriod ResolveWeeklyPeriod(DateTime date, int periodStart)
    {
        var currentIndex = ResolveMondayBasedDayOfWeek(date);
        var daysSinceStart = (currentIndex - periodStart + 7) % 7;
        var start = date.AddDays(-daysSinceStart);
        return new BudgetAllocationPeriod(start, start.AddDays(6));
    }

    private static BudgetAllocationPeriod ResolveBiweeklyPeriod(DateTime date, int periodStart)
    {
        var shiftedAnchor = BiweeklyAnchor.AddDays(periodStart - 1);
        var periodIndex = (int)Math.Floor((date - shiftedAnchor).TotalDays / 14d);
        var start = shiftedAnchor.AddDays(periodIndex * 14);
        return new BudgetAllocationPeriod(start, start.AddDays(13));
    }

    private static BudgetAllocationPeriod ResolveMonthlyPeriod(DateTime date, int periodStart)
    {
        var start = new DateTime(date.Year, date.Month, periodStart);
        if (date.Day < periodStart)
            start = start.AddMonths(-1);

        return new BudgetAllocationPeriod(start, start.AddMonths(1).AddDays(-1));
    }

    private static BudgetAllocationPeriod ResolveQuarterlyPeriod(DateTime date, int periodStart)
    {
        var quarterStartMonth = ((date.Month - 1) / 3 * 3) + 1;
        var startMonth = quarterStartMonth + periodStart - 1;
        var start = new DateTime(date.Year, startMonth, 1);
        if (date < start)
            start = start.AddMonths(-3);

        return new BudgetAllocationPeriod(start, start.AddMonths(3).AddDays(-1));
    }

    private static BudgetAllocationPeriod ResolveYearlyPeriod(DateTime date, int periodStart)
    {
        var start = new DateTime(date.Year, periodStart, 1);
        if (date < start)
            start = start.AddYears(-1);

        return new BudgetAllocationPeriod(start, start.AddYears(1).AddDays(-1));
    }
}
