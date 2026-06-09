using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxo.Core.DTO;
using Fluxo.Core.Interfaces.Services;

namespace Fluxo.ViewModels.Shell.Main;

public sealed partial class CalendarVM : ObservableObject, IDisposable
{
    private readonly ICalendarService _calendarService;
    private readonly DateOnly _currentDate;
    private CancellationTokenSource? _loadCts;
    private int _loadRequestVersion;
    private DateOnly _firstVisibleWeekStart;
    private DateOnly _visibleMonth;

    [ObservableProperty] private DateOnly _selectedDate;
    [ObservableProperty] private string _visibleMonthLabel = string.Empty;
    [ObservableProperty] private string _totalSpentText = "0";
    [ObservableProperty] private string _totalEarnedText = "0";
    [ObservableProperty] private string _goalsDueText = "0";
    [ObservableProperty] private string _paymentDueText = "0";
    [ObservableProperty] private IReadOnlyList<CalendarExpenseListItem> _expenses = [];
    [ObservableProperty] private IReadOnlyList<CalendarIncomeListItem> _incomes = [];
    [ObservableProperty] private IReadOnlyList<CalendarGoalDeadlineListItem> _goalDeadlines = [];
    [ObservableProperty] private IReadOnlyList<CalendarRecurringTransactionListItem> _recurringTransactions = [];
    [ObservableProperty] private bool _isLoading;

    public CalendarVM(ICalendarService calendarService)
        : this(calendarService, DateTime.Today)
    {
    }

    internal CalendarVM(ICalendarService calendarService, DateTime currentDate)
    {
        _calendarService = calendarService;
        _currentDate = DateOnly.FromDateTime(currentDate.Date);
        SelectedDate = _currentDate;
        _visibleMonth = new DateOnly(SelectedDate.Year, SelectedDate.Month, 1);
        _firstVisibleWeekStart = StartOfWeek(_visibleMonth);
        RebuildVisibleWeeks();
    }

    public ObservableCollection<CalendarWeekRow> VisibleWeeks { get; } = [];

    public bool HasNoExpenses => Expenses.Count == 0;
    public bool HasNoIncomes => Incomes.Count == 0;
    public bool HasNoGoalDeadlines => GoalDeadlines.Count == 0;
    public bool HasNoRecurringTransactions => RecurringTransactions.Count == 0;

    [RelayCommand]
    private async Task SelectDate(CalendarDayItem day)
    {
        await SelectDateAsync(day.Date);
    }

    [RelayCommand]
    private void ScrollCalendarRows(int delta)
    {
        if (delta == 0)
            return;

        _firstVisibleWeekStart = _firstVisibleWeekStart.AddDays(delta > 0 ? 7 : -7);
        _visibleMonth = ResolveDominantVisibleMonth(_firstVisibleWeekStart);
        RebuildVisibleWeeks();
    }

    [RelayCommand]
    private void NavigateToPreviousMonth()
    {
        SetVisibleMonth(_visibleMonth.AddMonths(-1));
    }

    [RelayCommand]
    private void NavigateToNextMonth()
    {
        SetVisibleMonth(_visibleMonth.AddMonths(1));
    }

    public Task LoadAsync(CancellationToken cancellationToken = default)
    {
        return SelectDateAsync(SelectedDate, cancellationToken);
    }

    public async Task SelectDateAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        SelectedDate = date;
        RebuildVisibleWeeks();

        _loadCts?.Cancel();
        var loadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loadCts = loadCts;
        var requestVersion = ++_loadRequestVersion;
        var token = loadCts.Token;

        IsLoading = true;
        try
        {
            var dto = await _calendarService.GetCalendarDayAsync(date, token);
            if (IsCurrentLoad(requestVersion, loadCts) && !token.IsCancellationRequested)
                Apply(dto);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (IsCurrentLoad(requestVersion, loadCts))
            {
                IsLoading = false;
                _loadCts = null;
            }

            loadCts.Dispose();
        }
    }

    private bool IsCurrentLoad(int requestVersion, CancellationTokenSource loadCts)
    {
        return requestVersion == _loadRequestVersion && ReferenceEquals(_loadCts, loadCts);
    }

    private void Apply(CalendarDto dto)
    {
        TotalSpentText = FormatMoney(dto.TotalSpent);
        TotalEarnedText = FormatMoney(dto.TotalEarned);
        GoalsDueText = dto.GoalsDue.ToString(CultureInfo.InvariantCulture);
        PaymentDueText = dto.PaymentsDue.ToString(CultureInfo.InvariantCulture);
        Expenses = dto.Expenses.Select(item => new CalendarExpenseListItem(item.Name, item.Amount, item.SpendingSourceName, item.TagName)).ToArray();
        Incomes = dto.Incomes.Select(item => new CalendarIncomeListItem(item.Name, item.Amount, item.SpendingSourceName)).ToArray();
        GoalDeadlines = dto.GoalDeadlines.Select(item => new CalendarGoalDeadlineListItem(item.Name, item.CurrentAmount, item.TargetAmount)).ToArray();
        RecurringTransactions = dto.RecurringTransactions.Select(item => new CalendarRecurringTransactionListItem(item.Name, item.Amount, item.Type.ToString(), item.SourceName)).ToArray();
        OnPropertyChanged(nameof(HasNoExpenses));
        OnPropertyChanged(nameof(HasNoIncomes));
        OnPropertyChanged(nameof(HasNoGoalDeadlines));
        OnPropertyChanged(nameof(HasNoRecurringTransactions));
    }

    private void RebuildVisibleWeeks()
    {
        VisibleWeeks.Clear();
        for (var weekIndex = 0; weekIndex < 6; weekIndex++)
        {
            var weekStart = _firstVisibleWeekStart.AddDays(weekIndex * 7);
            var days = Enumerable.Range(0, 7)
                .Select(offset =>
                {
                    var date = weekStart.AddDays(offset);
                    return new CalendarDayItem(
                        date,
                        date.Day.ToString(CultureInfo.InvariantCulture),
                        date == SelectedDate,
                        date == _currentDate,
                        date.Month != _visibleMonth.Month || date.Year != _visibleMonth.Year);
                })
                .ToArray();
            VisibleWeeks.Add(new CalendarWeekRow(days));
        }

        VisibleMonthLabel = _visibleMonth.ToDateTime(TimeOnly.MinValue).ToString("MMMM yyyy", CultureInfo.InvariantCulture);
    }

    private void SetVisibleMonth(DateOnly month)
    {
        _visibleMonth = new DateOnly(month.Year, month.Month, 1);
        _firstVisibleWeekStart = StartOfWeek(_visibleMonth);
        RebuildVisibleWeeks();
    }

    private static DateOnly StartOfWeek(DateOnly date)
    {
        var offset = (int)date.DayOfWeek;
        return date.AddDays(-offset);
    }

    private static DateOnly ResolveDominantVisibleMonth(DateOnly firstVisibleWeekStart)
    {
        var midpoint = firstVisibleWeekStart.AddDays(20);
        return new DateOnly(midpoint.Year, midpoint.Month, 1);
    }

    private static string FormatMoney(decimal value)
    {
        return value.ToString("N0", CultureInfo.InvariantCulture);
    }

    public void Dispose()
    {
        _loadCts?.Cancel();
        _loadCts = null;
    }
}

public sealed record CalendarWeekRow(IReadOnlyList<CalendarDayItem> Days);

public sealed record CalendarDayItem(
    DateOnly Date,
    string DayNumber,
    bool IsSelected,
    bool IsCurrentDay,
    bool IsOutsideVisibleMonth);

public sealed record CalendarExpenseListItem(
    string Name,
    decimal Amount,
    string SpendingSourceName,
    string? TagName)
{
    public string AmountText => $"${Amount:N0}";
}

public sealed record CalendarIncomeListItem(
    string Name,
    decimal Amount,
    string SpendingSourceName)
{
    public string AmountText => $"${Amount:N0}";
}

public sealed record CalendarGoalDeadlineListItem(
    string Name,
    decimal CurrentAmount,
    decimal TargetAmount)
{
    public string ProgressText => $"${CurrentAmount:N0} / ${TargetAmount:N0}";
}

public sealed record CalendarRecurringTransactionListItem(
    string Name,
    decimal Amount,
    string TypeText,
    string SourceName)
{
    public string AmountText => $"${Amount:N0}";
}
