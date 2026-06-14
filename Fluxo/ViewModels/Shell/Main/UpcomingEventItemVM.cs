using System.Globalization;

namespace Fluxo.ViewModels.Shell.Main;

public sealed class UpcomingEventItemVM(DateOnly date, string title, string amountText, string eventTypeText)
{
    public DateOnly Date { get; } = date;
    public string MonthText => Date.ToString("MMM", CultureInfo.InvariantCulture).ToUpperInvariant();
    public string DayText => Date.ToString("dd", CultureInfo.InvariantCulture);
    public string Title { get; } = title;
    public string AmountText { get; } = amountText;
    public string EventTypeText { get; } = eventTypeText;
}
