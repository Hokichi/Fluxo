using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
    private const double CalendarScrollSmoothing = 10d;
    private const double CalendarScrollSettledThreshold = 0.5d;
    private readonly CalendarVM _viewModel;
    private readonly SemaphoreSlim _openPreparationGate = new(1, 1);
    private double _calendarScrollOffset;
    private double _targetCalendarScrollOffset;
    private TimeSpan? _lastCalendarScrollRenderTime;
    private bool _isCalendarScrollAnimationRunning;
    private bool _isCalendarScrollResetQueued;

    public Calendar(CalendarVM viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnCalendarLoaded;
        SizeChanged += OnCalendarSizeChanged;
        Unloaded += OnUnloaded;
        PreviewKeyDown += OnCalendarPreviewKeyDown;
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
            QueueCalendarKeyboardFocus();
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

        _targetCalendarScrollOffset -= e.Delta * MouseWheelPixelsPerDelta;
        StartCalendarScrollAnimation();
        e.Handled = true;
    }

    private async void OnCalendarPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Home && Keyboard.Modifiers == ModifierKeys.Control)
        {
            ResetCalendarScrollOffset();
            await _viewModel.SelectCurrentDateAsync();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers != ModifierKeys.None)
            return;

        switch (e.Key)
        {
            case Key.Left:
                await _viewModel.SelectRelativeDateAsync(-1);
                e.Handled = true;
                break;

            case Key.Right:
                await _viewModel.SelectRelativeDateAsync(1);
                e.Handled = true;
                break;

            case Key.Up:
                await _viewModel.SelectRelativeDateAsync(-7);
                e.Handled = true;
                break;

            case Key.Down:
                await _viewModel.SelectRelativeDateAsync(7);
                e.Handled = true;
                break;
        }
    }

    private void OnCalendarLoaded(object sender, RoutedEventArgs e)
    {
        UpdateExpandedCalendarLayout();
        QueueCalendarScrollOffsetReset();
        QueueCalendarKeyboardFocus();
    }

    private void OnCalendarDayButtonClick(object sender, RoutedEventArgs e)
    {
        QueueCalendarKeyboardFocus();
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
        PreviewKeyDown -= OnCalendarPreviewKeyDown;
        StopCalendarScrollAnimation();
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
            _targetCalendarScrollOffset -= rowHeight;
            _calendarScrollOffset -= rowHeight;
        }

        while (_calendarScrollOffset <= -rowHeight)
        {
            _viewModel.ShiftCalendarFrameRowsCommand.Execute(-1);
            _targetCalendarScrollOffset += rowHeight;
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
        StopCalendarScrollAnimation();
        _targetCalendarScrollOffset = 0;
        _calendarScrollOffset = 0;
        ApplyCalendarScrollOffset();
    }

    private void StartCalendarScrollAnimation()
    {
        if (_isCalendarScrollAnimationRunning)
            return;

        _lastCalendarScrollRenderTime = null;
        _isCalendarScrollAnimationRunning = true;
        CompositionTarget.Rendering += OnCalendarScrollRendering;
    }

    private void StopCalendarScrollAnimation()
    {
        if (!_isCalendarScrollAnimationRunning)
            return;

        CompositionTarget.Rendering -= OnCalendarScrollRendering;
        _isCalendarScrollAnimationRunning = false;
        _lastCalendarScrollRenderTime = null;
    }

    private void OnCalendarScrollRendering(object? sender, EventArgs e)
    {
        if (e is not RenderingEventArgs renderingEventArgs)
            return;

        var elapsed = _lastCalendarScrollRenderTime is { } lastRenderTime
            ? renderingEventArgs.RenderingTime - lastRenderTime
            : TimeSpan.FromMilliseconds(16);
        _lastCalendarScrollRenderTime = renderingEventArgs.RenderingTime;

        if (elapsed <= TimeSpan.Zero)
            return;

        var rowHeight = GetCalendarRowHeight();
        if (rowHeight <= 0)
            return;

        var distanceToTarget = _targetCalendarScrollOffset - _calendarScrollOffset;
        if (Math.Abs(distanceToTarget) <= CalendarScrollSettledThreshold)
        {
            _calendarScrollOffset = _targetCalendarScrollOffset;
            NormalizeCalendarScrollOffset(rowHeight);
            ApplyCalendarScrollOffset();
            StopCalendarScrollAnimation();
            return;
        }

        var progress = 1d - Math.Exp(-CalendarScrollSmoothing * elapsed.TotalSeconds);
        _calendarScrollOffset += distanceToTarget * progress;
        NormalizeCalendarScrollOffset(rowHeight);
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

    private void QueueCalendarKeyboardFocus()
    {
        Dispatcher.BeginInvoke(
            () =>
            {
                FocusCalendarKeyboardTarget();
            },
            DispatcherPriority.Input);
    }

    private void FocusCalendarKeyboardTarget()
    {
        if (!IsVisible)
            return;

        CalendarWeekViewport.Focus();
        Keyboard.Focus(CalendarWeekViewport);
    }
}
