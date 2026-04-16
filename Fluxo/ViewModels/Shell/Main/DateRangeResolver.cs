namespace Fluxo.ViewModels.Shell;

public sealed record DateRange(DateTime From, DateTime To);

public static class DateRangeResolver
{
    public static DateRange Resolve(DateTime selectedDate, MainContentViewMode viewMode)
    {
        var date = selectedDate.Date;

        return viewMode switch
        {
            MainContentViewMode.Daily => new DateRange(date, date),
            MainContentViewMode.Weekly =>
                new DateRange(GetStartOfWeek(date), GetStartOfWeek(date).AddDays(6)),
            MainContentViewMode.Monthly => new DateRange(
                CreateDate(date.Year, date.Month, 1, date.Kind),
                CreateDate(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month), date.Kind)),
            MainContentViewMode.AllTime => throw new InvalidOperationException(
                "All-time view does not have a bounded date range."),
            _ => throw new InvalidOperationException($"Unsupported view mode: {viewMode}.")
        };
    }

    private static DateTime GetStartOfWeek(DateTime date)
    {
        var dayOfWeek = (int)date.DayOfWeek;
        var mondayOffset = dayOfWeek == 0 ? -6 : 1 - dayOfWeek;

        return date.AddDays(mondayOffset);
    }

    private static DateTime CreateDate(int year, int month, int day, DateTimeKind kind)
    {
        return new DateTime(year, month, day, 0, 0, 0, kind);
    }
}
