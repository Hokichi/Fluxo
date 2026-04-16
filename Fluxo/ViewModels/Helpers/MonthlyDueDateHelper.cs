namespace Fluxo.ViewModels.Helpers;

public static class MonthlyDueDateHelper
{
    public static DateTime? ResolveUpcomingDate(int monthlyDueDate, DateTime today)
    {
        if (monthlyDueDate is < 1 or > 31)
            return null;

        var currentMonthDay = Math.Min(monthlyDueDate, DateTime.DaysInMonth(today.Year, today.Month));
        var dueDate = new DateTime(today.Year, today.Month, currentMonthDay);
        if (dueDate.Date >= today.Date)
            return dueDate;

        var nextMonth = today.AddMonths(1);
        var nextMonthDay = Math.Min(monthlyDueDate, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month));
        return new DateTime(nextMonth.Year, nextMonth.Month, nextMonthDay);
    }

    public static DateTime? ToPickerDate(int monthlyDueDate)
    {
        return ResolveUpcomingDate(monthlyDueDate, DateTime.Today);
    }
}
