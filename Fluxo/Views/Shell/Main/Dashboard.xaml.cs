using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Fluxo.Services.Dialogs;
using DashboardVM = Fluxo.ViewModels.Shell.Main.DashboardVM;

namespace Fluxo.Views.Shell.Main;

public partial class Dashboard : UserControl
{
    private const double DashboardSpendingSourcesScrollPixels = 10;
    private const int DashboardSpendingSourcesScrollIntervalMilliseconds = 10;

    private readonly IDialogService _dialogService;
    private readonly DispatcherTimer _dashboardSpendingSourcesScrollTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(DashboardSpendingSourcesScrollIntervalMilliseconds)
    };

    private int _dashboardSpendingSourcesScrollDirection;

    public Dashboard(DashboardVM dashboardVM, IDialogService dialogService)
    {
        _dialogService = dialogService;
        InitializeComponent();
        DataContext = dashboardVM;
        _dashboardSpendingSourcesScrollTimer.Tick += OnDashboardSpendingSourcesScrollTimerTick;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _ = Dispatcher.BeginInvoke(
            UpdateDashboardSpendingSourcesScrollButtonVisibility,
            DispatcherPriority.ApplicationIdle);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _dashboardSpendingSourcesScrollTimer.Stop();
    }

    private void OnDashboardSpendingSourcesScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        UpdateDashboardSpendingSourcesScrollButtonVisibility();
    }

    private void OnDashboardSpendingSourcesScrollViewerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        _ = Dispatcher.BeginInvoke(
            UpdateDashboardSpendingSourcesScrollButtonVisibility,
            DispatcherPriority.ApplicationIdle);
    }

    private void OnDashboardSpendingSourcesScrollLeftButtonPressed(object sender, MouseButtonEventArgs e)
    {
        StartDashboardSpendingSourcesScroll(sender, e, -1);
    }

    private void OnDashboardSpendingSourcesScrollRightButtonPressed(object sender, MouseButtonEventArgs e)
    {
        StartDashboardSpendingSourcesScroll(sender, e, 1);
    }

    private void OnDashboardSpendingSourcesScrollButtonReleased(object sender, MouseButtonEventArgs e)
    {
        StopDashboardSpendingSourcesScroll(sender);
        e.Handled = true;
    }

    private void OnDashboardSpendingSourcesScrollButtonLostMouseCapture(object sender, MouseEventArgs e)
    {
        StopDashboardSpendingSourcesScroll(sender);
    }

    private void OnDashboardSpendingSourcesScrollTimerTick(object? sender, EventArgs e)
    {
        if (_dashboardSpendingSourcesScrollDirection == 0)
            return;

        ScrollDashboardSpendingSources(_dashboardSpendingSourcesScrollDirection);
    }

    private void StartDashboardSpendingSourcesScroll(object sender, MouseButtonEventArgs e, int direction)
    {
        if (DashboardSpendingSourcesScrollViewer.ScrollableWidth <= 0)
            return;

        _dashboardSpendingSourcesScrollDirection = direction;

        if (sender is UIElement scrollButton)
            scrollButton.CaptureMouse();

        _dashboardSpendingSourcesScrollTimer.Stop();
        _dashboardSpendingSourcesScrollTimer.Start();
        e.Handled = true;
    }

    private void StopDashboardSpendingSourcesScroll(object sender)
    {
        _dashboardSpendingSourcesScrollTimer.Stop();
        _dashboardSpendingSourcesScrollDirection = 0;

        if (sender is UIElement { IsMouseCaptured: true } scrollButton)
            scrollButton.ReleaseMouseCapture();
    }

    private void ScrollDashboardSpendingSources(int direction)
    {
        if (DashboardSpendingSourcesScrollViewer.ScrollableWidth <= 0)
            return;

        var targetOffset = Math.Clamp(
            DashboardSpendingSourcesScrollViewer.HorizontalOffset + direction * DashboardSpendingSourcesScrollPixels,
            0,
            DashboardSpendingSourcesScrollViewer.ScrollableWidth);

        DashboardSpendingSourcesScrollViewer.ScrollToHorizontalOffset(targetOffset);
        UpdateDashboardSpendingSourcesScrollButtonVisibility();
    }

    private void UpdateDashboardSpendingSourcesScrollButtonVisibility()
    {
        if (DashboardSpendingSourcesScrollLeftButton is null ||
            DashboardSpendingSourcesScrollRightButton is null ||
            DashboardSpendingSourcesScrollViewer is null)
            return;

        var canScroll = DashboardSpendingSourcesScrollViewer.ScrollableWidth > 0;
        var canScrollLeft = canScroll &&
                            DashboardSpendingSourcesScrollViewer.HorizontalOffset > 0;
        var canScrollRight = canScroll &&
                             DashboardSpendingSourcesScrollViewer.HorizontalOffset < DashboardSpendingSourcesScrollViewer.ScrollableWidth;

        DashboardSpendingSourcesScrollLeftButton.Visibility = canScrollLeft
            ? Visibility.Visible
            : Visibility.Collapsed;
        DashboardSpendingSourcesScrollRightButton.Visibility = canScrollRight
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void OnSpendingSourcesButtonClick(object sender, RoutedEventArgs e)
    {
        _dialogService.ShowSpendingSourcesList(Window.GetWindow(this));
    }

    private void OnAddSpendingSourceButtonClick(object sender, RoutedEventArgs e)
    {
        _dialogService.ShowAddSpendingSource(Window.GetWindow(this));
    }
}
