using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace Fluxo.Resources.Components;

public partial class DateSelector : UserControl, INotifyPropertyChanged
{
    public static readonly DependencyProperty SelectedDateProperty =
        DependencyProperty.Register(
            nameof(SelectedDate),
            typeof(DateTime),
            typeof(DateSelector),
            new FrameworkPropertyMetadata(
                DateTime.Today,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnSelectedDateChanged));

    public static readonly DependencyProperty MaxSelectableDateProperty =
        DependencyProperty.Register(
            nameof(MaxSelectableDate),
            typeof(DateTime?),
            typeof(DateSelector),
            new FrameworkPropertyMetadata(null, OnMaxSelectableDateChanged));

    private DisplayMode _displayMode = DisplayMode.Day;
    private DateTime _displayMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private bool _isPopupOpen;
    private int _yearRangeStart;

    public DateSelector()
    {
        InitializeComponent();
        _yearRangeStart = SelectedDate.Year - 5;
        Loaded += (_, _) => RebuildView();
    }

    public ObservableCollection<CalendarDayItem> Days { get; } = [];
    public ObservableCollection<CalendarChoiceItem> Months { get; } = [];
    public ObservableCollection<CalendarChoiceItem> Years { get; } = [];

    public DateTime SelectedDate
    {
        get => (DateTime)GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    public DateTime? MaxSelectableDate
    {
        get => (DateTime?)GetValue(MaxSelectableDateProperty);
        set => SetValue(MaxSelectableDateProperty, value);
    }

    public bool IsPopupOpen
    {
        get => _isPopupOpen;
        set
        {
            if (_isPopupOpen == value)
                return;

            _isPopupOpen = value;

            if (value)
            {
                _displayMode = DisplayMode.Day;
                _displayMonth = new DateTime(SelectedDate.Year, SelectedDate.Month, 1);
                _yearRangeStart = SelectedDate.Year - 5;
                OnPropertyChanged(nameof(IsDayMode));
                OnPropertyChanged(nameof(IsMonthMode));
                OnPropertyChanged(nameof(IsYearMode));
                OnPropertyChanged(nameof(DisplayMonthLabel));
                OnPropertyChanged(nameof(DisplayYearLabel));
                OnPropertyChanged(nameof(DisplayYearRangeLabel));
                RebuildView();
            }

            OnPropertyChanged();
        }
    }

    public string FormattedSelectedDate => SelectedDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
    public string DisplayMonthLabel => _displayMonth.ToString("MMMM", CultureInfo.InvariantCulture);
    public string DisplayYearLabel => _displayMonth.ToString("yyyy", CultureInfo.InvariantCulture);
    public string DisplayYearRangeLabel => $"{_yearRangeStart} - {_yearRangeStart + 11}";
    public bool IsDayMode => _displayMode == DisplayMode.Day;
    public bool IsMonthMode => _displayMode == DisplayMode.Month;
    public bool IsYearMode => _displayMode == DisplayMode.Year;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void RebuildViewForTests()
    {
        RebuildView();
    }

    private static void OnSelectedDateChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not DateSelector selector || e.NewValue is not DateTime selectedDate)
            return;

        if (selector.MaxSelectableDate is DateTime maxDate && selectedDate.Date > maxDate.Date)
        {
            selector.SelectedDate = maxDate.Date;
            return;
        }

        selector._displayMonth = new DateTime(selectedDate.Year, selectedDate.Month, 1);
        selector._yearRangeStart = selectedDate.Year - 5;
        selector.OnPropertyChanged(nameof(FormattedSelectedDate));
        selector.OnPropertyChanged(nameof(DisplayMonthLabel));
        selector.OnPropertyChanged(nameof(DisplayYearLabel));
        selector.OnPropertyChanged(nameof(DisplayYearRangeLabel));
        selector.RebuildView();
    }

    private static void OnMaxSelectableDateChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not DateSelector selector)
            return;

        if (selector.MaxSelectableDate is DateTime maxDate && selector.SelectedDate.Date > maxDate.Date)
        {
            selector.SelectedDate = maxDate.Date;
            return;
        }

        selector.RebuildView();
    }

    private void OnPreviousClick(object sender, RoutedEventArgs e)
    {
        switch (_displayMode)
        {
            case DisplayMode.Day:
                _displayMonth = _displayMonth.AddMonths(-1);
                break;
            case DisplayMode.Month:
                _displayMonth = _displayMonth.AddYears(-1);
                break;
            case DisplayMode.Year:
                _yearRangeStart -= 12;
                break;
        }

        RefreshHeaderText();
        RebuildView();
    }

    private void OnNextClick(object sender, RoutedEventArgs e)
    {
        switch (_displayMode)
        {
            case DisplayMode.Day:
                _displayMonth = _displayMonth.AddMonths(1);
                break;
            case DisplayMode.Month:
                _displayMonth = _displayMonth.AddYears(1);
                break;
            case DisplayMode.Year:
                _yearRangeStart += 12;
                break;
        }

        RefreshHeaderText();
        RebuildView();
    }

    private void OnMonthHeaderClick(object sender, RoutedEventArgs e)
    {
        SetDisplayMode(DisplayMode.Month);
    }

    private void OnYearHeaderClick(object sender, RoutedEventArgs e)
    {
        _yearRangeStart = _displayMonth.Year - 5;
        SetDisplayMode(DisplayMode.Year);
    }

    private void OnDayButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: DateTime selectedDate })
            return;

        if (!IsSelectable(selectedDate))
            return;

        SelectedDate = selectedDate.Date;
        IsPopupOpen = false;
    }

    private void OnMonthButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int month })
            return;

        if (!IsMonthSelectable(_displayMonth.Year, month))
            return;

        _displayMonth = new DateTime(_displayMonth.Year, month, 1);
        SetDisplayMode(DisplayMode.Day);
        RefreshHeaderText();
        RebuildView();
    }

    private void OnYearButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int year })
            return;

        if (!IsYearSelectable(year))
            return;

        _displayMonth = new DateTime(year, _displayMonth.Month, 1);
        SetDisplayMode(DisplayMode.Month);
        RefreshHeaderText();
        RebuildView();
    }

    private void SetDisplayMode(DisplayMode mode)
    {
        if (_displayMode == mode)
            return;

        _displayMode = mode;
        OnPropertyChanged(nameof(IsDayMode));
        OnPropertyChanged(nameof(IsMonthMode));
        OnPropertyChanged(nameof(IsYearMode));
    }

    private void RefreshHeaderText()
    {
        OnPropertyChanged(nameof(DisplayMonthLabel));
        OnPropertyChanged(nameof(DisplayYearLabel));
        OnPropertyChanged(nameof(DisplayYearRangeLabel));
    }

    private void RebuildView()
    {
        BuildCalendarDays();
        BuildMonths();
        BuildYears();
    }

    private void BuildCalendarDays()
    {
        Days.Clear();

        var firstDayOfMonth = new DateTime(_displayMonth.Year, _displayMonth.Month, 1);
        var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);
        var leadingDayOffset = ((int)firstDayOfMonth.DayOfWeek + 6) % 7;
        var totalCells = leadingDayOffset + lastDayOfMonth.Day;
        var totalVisibleDays = (totalCells + 6) / 7 * 7;
        var firstVisibleDay = firstDayOfMonth.AddDays(-leadingDayOffset);

        for (var index = 0; index < totalVisibleDays; index++)
        {
            var currentDay = firstVisibleDay.AddDays(index);
            Days.Add(new CalendarDayItem(
                currentDay,
                currentDay.Day.ToString(CultureInfo.InvariantCulture),
                currentDay.Month == _displayMonth.Month,
                currentDay.Date == DateTime.Today,
                currentDay.Date == SelectedDate.Date,
                IsSelectable(currentDay)));
        }
    }

    private void BuildMonths()
    {
        Months.Clear();

        for (var month = 1; month <= 12; month++)
        {
            var monthDate = new DateTime(_displayMonth.Year, month, 1);
            Months.Add(new CalendarChoiceItem(
                monthDate.ToString("MMM", CultureInfo.InvariantCulture),
                month,
                SelectedDate.Year == _displayMonth.Year && SelectedDate.Month == month,
                IsMonthSelectable(_displayMonth.Year, month)));
        }
    }

    private void BuildYears()
    {
        Years.Clear();

        for (var year = _yearRangeStart; year < _yearRangeStart + 12; year++)
            Years.Add(new CalendarChoiceItem(
                year.ToString(CultureInfo.InvariantCulture),
                year,
                SelectedDate.Year == year,
                IsYearSelectable(year)));
    }

    private bool IsSelectable(DateTime date)
    {
        return MaxSelectableDate is not DateTime maxDate || date.Date <= maxDate.Date;
    }

    private bool IsMonthSelectable(int year, int month)
    {
        if (MaxSelectableDate is not DateTime maxDate)
            return true;

        var firstDayOfMonth = new DateTime(year, month, 1);
        var maxMonth = new DateTime(maxDate.Year, maxDate.Month, 1);
        return firstDayOfMonth <= maxMonth;
    }

    private bool IsYearSelectable(int year)
    {
        return MaxSelectableDate is not DateTime maxDate || year <= maxDate.Year;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private enum DisplayMode
    {
        Day,
        Month,
        Year
    }

    public sealed record CalendarDayItem(
        DateTime Date,
        string DayNumber,
        bool IsCurrentMonth,
        bool IsToday,
        bool IsSelected,
        bool IsEnabled);

    public sealed record CalendarChoiceItem(
        string Label,
        int Value,
        bool IsSelected,
        bool IsEnabled);
}
