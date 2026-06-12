using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Fluxo.ViewModels.Shell.Main;

namespace Fluxo.Views.Shell.Main.Pages;

public partial class Calendar : UserControl
{
    public static readonly DependencyProperty UseExpandedCalendarLayoutProperty = DependencyProperty.Register(
        nameof(UseExpandedCalendarLayout),
        typeof(bool),
        typeof(Calendar),
        new PropertyMetadata(false));

    private const double ExpandedLayoutMinWidth = 1320;
    private const int CalendarFrameWeekCount = 6;
    private const int CalendarBufferWeekCount = 2;
    private const int CalendarBufferedWeekCount = CalendarFrameWeekCount + CalendarBufferWeekCount * 2;
    private const double MouseWheelPixelsPerDelta = 48d / 120d;
    private readonly CalendarVM _viewModel;
    private readonly SemaphoreSlim _openPreparationGate = new(1, 1);
    private double _calendarScrollOffset;
    private bool _isCalendarScrollResetQueued;

    public Calendar(CalendarVM viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnCalendarLoaded;
        SizeChanged += OnCalendarSizeChanged;
        Unloaded += OnUnloaded;
    }

    public bool UseExpandedCalendarLayout
    {
        get => (bool)GetValue(UseExpandedCalendarLayoutProperty);
        private set => SetValue(UseExpandedCalendarLayoutProperty, value);
    }

    public async Task PrepareForOpenAsync(CancellationToken cancellationToken = default)
    {
        await _openPreparationGate.WaitAsync(cancellationToken);
        try
        {
            await _viewModel.LoadAsync(cancellationToken);
        }
        finally
        {
            _openPreparationGate.Release();
        }
    }

    private void OnCalendarMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        var rowHeight = GetCalendarRowHeight();
        if (rowHeight <= 0)
            return;

        _calendarScrollOffset -= e.Delta * MouseWheelPixelsPerDelta;
        NormalizeCalendarScrollOffset(rowHeight);
        ApplyCalendarScrollOffset();
        e.Handled = true;
    }

    private void OnCalendarLoaded(object sender, RoutedEventArgs e)
    {
        UpdateExpandedCalendarLayout();
        QueueCalendarScrollOffsetReset();
    }

    private void OnCalendarSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateExpandedCalendarLayout();
        QueueCalendarScrollOffsetReset();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        Loaded -= OnCalendarLoaded;
        SizeChanged -= OnCalendarSizeChanged;
        CalendarWeekViewport.SizeChanged -= OnCalendarWeekViewportSizeChanged;
        Unloaded -= OnUnloaded;
        _viewModel.Dispose();
    }

    private void UpdateExpandedCalendarLayout()
    {
        UseExpandedCalendarLayout = GetIsWindowLayoutMaximized() || ActualWidth >= ExpandedLayoutMinWidth;
    }

    private bool GetIsWindowLayoutMaximized()
    {
        return Window.GetWindow(this) is MainWindow { IsWindowLayoutMaximized: true };
    }

    private void OnCalendarWeekViewportSizeChanged(object sender, SizeChangedEventArgs e)
    {
        QueueCalendarScrollOffsetReset();
    }

    private double GetCalendarRowHeight()
    {
        if (CalendarWeekViewport.ActualHeight <= 0)
            return 0;

        return CalendarWeekViewport.ActualHeight / CalendarFrameWeekCount;
    }

    private void NormalizeCalendarScrollOffset(double rowHeight)
    {
        while (_calendarScrollOffset >= rowHeight)
        {
            _viewModel.ShiftCalendarFrameRowsCommand.Execute(1);
            _calendarScrollOffset -= rowHeight;
        }

        while (_calendarScrollOffset <= -rowHeight)
        {
            _viewModel.ShiftCalendarFrameRowsCommand.Execute(-1);
            _calendarScrollOffset += rowHeight;
        }
    }

    private void ApplyCalendarScrollOffset()
    {
        var rowHeight = GetCalendarRowHeight();
        if (rowHeight <= 0)
            return;

        CalendarWeekItemsControl.Height = rowHeight * CalendarBufferedWeekCount;
        CalendarWeekTranslateTransform.Y = -(CalendarBufferWeekCount * rowHeight) - _calendarScrollOffset;
    }

    private void ResetCalendarScrollOffset()
    {
        _calendarScrollOffset = 0;
        ApplyCalendarScrollOffset();
    }

    private void QueueCalendarScrollOffsetReset()
    {
        if (_isCalendarScrollResetQueued)
            return;

        _isCalendarScrollResetQueued = true;
        Dispatcher.BeginInvoke(
            () =>
            {
                _isCalendarScrollResetQueued = false;
                ResetCalendarScrollOffset();
            },
            DispatcherPriority.Loaded);
    }
}
