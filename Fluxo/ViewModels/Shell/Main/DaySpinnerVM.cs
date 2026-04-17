using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.ViewModels.Controls;
using Fluxo.ViewModels.Messages;

namespace Fluxo.ViewModels.Shell;

public partial class DaySpinnerVM : ObservableRecipient,
    IRecipient<ViewModeChangeMessage>,
    IRecipient<MoveToCurrentPeriodRequestedMessage>
{
    private bool _suppressSelectionPublishing;
    private MainContentViewMode _selectedMainContentViewMode = MainContentViewMode.Daily;
    private int _spinnerPageOffset;

    public DaySpinnerVM(IMessenger? messenger = null)
        : base(messenger ?? WeakReferenceMessenger.Default)
    {
        BuildSpinnerForMode(DateTime.Today, publishSelection: false);
        IsActive = true;
    }

    [ObservableProperty]
    private ObservableCollection<DayOfWeekVM> _daysOfWeek = [];

    [ObservableProperty]
    private DayOfWeekVM _selectedDay = new();

    [ObservableProperty]
    private bool _canNavigateForward;

    [ObservableProperty]
    private bool _isSpinnerVisible = true;

    [ObservableProperty]
    private bool _isAtCurrentPeriod = true;

    public string MoveToCurrentLabel => _selectedMainContentViewMode switch
    {
        MainContentViewMode.Daily => "Move to today",
        MainContentViewMode.Weekly => "Move to current week",
        MainContentViewMode.Monthly => "Move to current month",
        _ => string.Empty
    };

    public void Receive(ViewModeChangeMessage message)
    {
        _selectedMainContentViewMode = message.Value;
        OnPropertyChanged(nameof(MoveToCurrentLabel));

        if (message.Value == MainContentViewMode.AllTime)
        {
            _suppressSelectionPublishing = true;
            try
            {
                IsSpinnerVisible = false;
                IsAtCurrentPeriod = false;
                CanNavigateForward = false;
                _spinnerPageOffset = 0;
                DaysOfWeek.Clear();
                SelectedDay = new DayOfWeekVM();
            }
            finally
            {
                _suppressSelectionPublishing = false;
            }

            PublishSpinnerPeriodState();
            Messenger.Send(new AllTimeViewModeMessage());
            return;
        }

        IsSpinnerVisible = true;
        BuildSpinnerForMode(DateTime.Today, publishSelection: true);
    }

    public void Receive(MoveToCurrentPeriodRequestedMessage message)
    {
        MoveToCurrentPeriodCore();
    }

    public void PublishCurrentSelection()
    {
        if (_selectedMainContentViewMode == MainContentViewMode.AllTime)
        {
            Messenger.Send(new AllTimeViewModeMessage());
            return;
        }

        PublishSelectedRange(SelectedDay.Date);
    }

    [RelayCommand]
    private void NavigateSpinnerBack()
    {
        _spinnerPageOffset--;
        CanNavigateForward = true;
        BuildSpinnerItems();
        SelectFirstSpinnerItem();
    }

    [RelayCommand]
    private void NavigateSpinnerForward()
    {
        if (_spinnerPageOffset >= 0)
            return;

        _spinnerPageOffset++;
        CanNavigateForward = _spinnerPageOffset < 0;
        BuildSpinnerItems();
        SelectFirstSpinnerItem();
    }

    [RelayCommand]
    private void MoveToCurrentPeriod()
    {
        MoveToCurrentPeriodCore();
    }

    partial void OnSelectedDayChanged(DayOfWeekVM value)
    {
        if (_suppressSelectionPublishing)
            return;

        if (value is null)
            return;

        foreach (var item in DaysOfWeek)
            item.IsSelected = ReferenceEquals(item, value);

        IsAtCurrentPeriod = EvaluateIsAtCurrentPeriod(value.Date);
        PublishSelectedRange(value.Date);
        PublishSpinnerPeriodState();
    }

    private void BuildSpinnerForMode(DateTime referenceDate, bool publishSelection)
    {
        ComputeSpinnerOffset(referenceDate);
        CanNavigateForward = _spinnerPageOffset < 0;
        BuildSpinnerItems();

        if (publishSelection)
            SelectSpinnerItemForDate(referenceDate);
        else
            SelectFirstSpinnerItem(publishSelection: false);
    }

    private void PublishSelectedRange(DateTime selectedDate)
    {
        if (_selectedMainContentViewMode == MainContentViewMode.AllTime)
            return;

        var range = DateRangeResolver.Resolve(selectedDate, _selectedMainContentViewMode);
        Messenger.Send(new DateRangeSelectionChangedMessage(range.From, range.To));
    }

    private void ComputeSpinnerOffset(DateTime referenceDate)
    {
        var today = DateTime.Today;

        switch (_selectedMainContentViewMode)
        {
            case MainContentViewMode.Daily:
                {
                    var todayMonday = GetMonday(today);
                    var refMonday = GetMonday(referenceDate);
                    _spinnerPageOffset = (refMonday - todayMonday).Days / 7;
                    break;
                }
            case MainContentViewMode.Weekly:
                {
                    _spinnerPageOffset = ComputeWeeklyPageOffset(today, referenceDate);
                    break;
                }
            case MainContentViewMode.Monthly:
                {
                    var todayGroupMonth = (today.Month - 1) / 4 * 4 + 1;
                    var refGroupMonth = (referenceDate.Month - 1) / 4 * 4 + 1;
                    var todayBase = new DateTime(today.Year, todayGroupMonth, 1);
                    var refBase = new DateTime(referenceDate.Year, refGroupMonth, 1);
                    _spinnerPageOffset = ((refBase.Year - todayBase.Year) * 12 + refBase.Month - todayBase.Month) / 4;
                    break;
                }
        }
    }

    private void BuildSpinnerItems()
    {
        switch (_selectedMainContentViewMode)
        {
            case MainContentViewMode.Daily:
                BuildDailySpinnerItems();
                break;

            case MainContentViewMode.Weekly:
                BuildWeeklySpinnerItems();
                break;

            case MainContentViewMode.Monthly:
                BuildMonthlySpinnerItems();
                break;
        }
    }

    private void BuildDailySpinnerItems()
    {
        var currentWeekMonday = GetMonday(DateTime.Today);
        var firstDay = currentWeekMonday.AddDays(_spinnerPageOffset * 7);

        DaysOfWeek = new ObservableCollection<DayOfWeekVM>(
            Enumerable.Range(0, 7).Select(offset =>
            {
                var day = firstDay.AddDays(offset);
                return new DayOfWeekVM
                {
                    Date = day,
                    DayName = day.ToString("ddd", CultureInfo.InvariantCulture),
                    DayNumber = day.Day.ToString(CultureInfo.InvariantCulture),
                    IsSelected = false
                };
            }));
    }

    private void BuildWeeklySpinnerItems()
    {
        var today = DateTime.Today;
        var baseMonday = GetWeeklyWindowStart(GetMonday(today));
        var firstMonday = baseMonday.AddDays(_spinnerPageOffset * 28);

        DaysOfWeek = new ObservableCollection<DayOfWeekVM>(
            Enumerable.Range(0, 4).Select(offset =>
            {
                var weekMonday = firstMonday.AddDays(offset * 7);
                var weekNumber = ISOWeek.GetWeekOfYear(weekMonday);
                return new DayOfWeekVM
                {
                    Date = weekMonday,
                    DayName = "Week",
                    DayNumber = weekNumber.ToString(CultureInfo.InvariantCulture),
                    IsSelected = false
                };
            }));
    }

    private void BuildMonthlySpinnerItems()
    {
        var today = DateTime.Today;
        var groupStartMonth = (today.Month - 1) / 4 * 4 + 1;
        var baseDate = new DateTime(today.Year, groupStartMonth, 1);
        var firstMonth = baseDate.AddMonths(_spinnerPageOffset * 4);

        DaysOfWeek = new ObservableCollection<DayOfWeekVM>(
            Enumerable.Range(0, 4).Select(offset =>
            {
                var monthDate = firstMonth.AddMonths(offset);
                return new DayOfWeekVM
                {
                    Date = monthDate,
                    DayName = monthDate.Year.ToString(CultureInfo.InvariantCulture),
                    DayNumber = monthDate.ToString("MMM", CultureInfo.InvariantCulture),
                    IsSelected = false
                };
            }));
    }

    private void SelectSpinnerItemForDate(DateTime referenceDate)
    {
        var match = _selectedMainContentViewMode switch
        {
            MainContentViewMode.Daily =>
                DaysOfWeek.FirstOrDefault(day => day.Date.Date == referenceDate.Date),

            MainContentViewMode.Weekly =>
                DaysOfWeek.FirstOrDefault(day =>
                    referenceDate.Date >= day.Date.Date && referenceDate.Date < day.Date.AddDays(7).Date),

            MainContentViewMode.Monthly =>
                DaysOfWeek.FirstOrDefault(day =>
                    day.Date.Year == referenceDate.Year && day.Date.Month == referenceDate.Month),

            _ => null
        };

        SelectDay(match ?? DaysOfWeek.FirstOrDefault() ?? new DayOfWeekVM(), publishSelection: true);
    }

    private void SelectFirstSpinnerItem(bool publishSelection = true)
    {
        SelectDay(DaysOfWeek.FirstOrDefault() ?? new DayOfWeekVM(), publishSelection);
    }

    private void SelectDay(DayOfWeekVM day, bool publishSelection)
    {
        _suppressSelectionPublishing = true;
        try
        {
            SelectedDay = day;
        }
        finally
        {
            _suppressSelectionPublishing = false;
        }

        foreach (var item in DaysOfWeek)
            item.IsSelected = ReferenceEquals(item, day);

        IsAtCurrentPeriod = EvaluateIsAtCurrentPeriod(day.Date);

        if (publishSelection)
            PublishSelectedRange(day.Date);

        PublishSpinnerPeriodState();
    }

    private void MoveToCurrentPeriodCore()
    {
        if (_selectedMainContentViewMode == MainContentViewMode.AllTime)
            return;

        BuildSpinnerForMode(DateTime.Today, publishSelection: true);
    }

    private bool EvaluateIsAtCurrentPeriod(DateTime selectedDate)
    {
        var today = DateTime.Today;
        var date = selectedDate.Date;

        return _selectedMainContentViewMode switch
        {
            MainContentViewMode.Daily => date == today,
            MainContentViewMode.Weekly => today >= date && today < date.AddDays(7),
            MainContentViewMode.Monthly => date.Year == today.Year && date.Month == today.Month,
            _ => false
        };
    }

    private void PublishSpinnerPeriodState()
    {
        Messenger.Send(new SpinnerPeriodStateChangedMessage(new SpinnerPeriodState(
            IsAtCurrentPeriod,
            IsSpinnerVisible,
            MoveToCurrentLabel)));
    }

    private static DateTime GetMonday(DateTime date)
    {
        var dayOfWeek = (int)date.DayOfWeek;
        var mondayOffset = dayOfWeek == 0 ? -6 : 1 - dayOfWeek;
        return date.Date.AddDays(mondayOffset);
    }

    internal static int ComputeWeeklyPageOffset(DateTime today, DateTime referenceDate)
    {
        var todayMonday = GetMonday(today);
        var referenceMonday = GetMonday(referenceDate);

        return GetWeeklyWindowIndex(referenceMonday) - GetWeeklyWindowIndex(todayMonday);
    }

    private static DateTime GetWeeklyWindowStart(DateTime mondayDate)
    {
        return DateTime.MinValue.Date.AddDays(GetWeeklyWindowIndex(mondayDate) * 28);
    }

    private static int GetWeeklyWindowIndex(DateTime mondayDate)
    {
        var daysSinceEpochMonday = (mondayDate.Date - DateTime.MinValue.Date).Days;
        return FloorDivide(daysSinceEpochMonday, 28);
    }

    private static int FloorDivide(int dividend, int divisor)
    {
        var quotient = dividend / divisor;
        var remainder = dividend % divisor;

        if (remainder != 0 && dividend < 0)
            quotient--;

        return quotient;
    }
}
