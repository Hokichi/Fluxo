using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Fluxo.Services.Dialogs;
using DashboardVM = Fluxo.ViewModels.Shell.Main.DashboardVM;

namespace Fluxo.Views.Shell.Main.Pages;

public partial class Dashboard : UserControl
{
    private const double DashboardAccountsScrollPixels = 10;
    private const int DashboardAccountsScrollIntervalMilliseconds = 10;

    private readonly IDialogService _dialogService;
    private readonly DispatcherTimer _dashboardAccountsScrollTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(DashboardAccountsScrollIntervalMilliseconds)
    };

    private int _dashboardAccountsScrollDirection;

    public Dashboard(DashboardVM dashboardVM, IDialogService dialogService)
    {
        _dialogService = dialogService;
        InitializeComponent();
        DataContext = dashboardVM;
        _dashboardAccountsScrollTimer.Tick += OnDashboardAccountsScrollTimerTick;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _ = Dispatcher.BeginInvoke(
            UpdateDashboardAccountsScrollButtonVisibility,
            DispatcherPriority.ApplicationIdle);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _dashboardAccountsScrollTimer.Stop();
    }

    private void OnDashboardAccountsScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        UpdateDashboardAccountsScrollButtonVisibility();
    }

    private void OnDashboardAccountsScrollViewerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        _ = Dispatcher.BeginInvoke(
            UpdateDashboardAccountsScrollButtonVisibility,
            DispatcherPriority.ApplicationIdle);
    }

    private void OnDashboardAccountsScrollLeftButtonPressed(object sender, MouseButtonEventArgs e)
    {
        StartDashboardAccountsScroll(sender, e, -1);
    }

    private void OnDashboardAccountsScrollRightButtonPressed(object sender, MouseButtonEventArgs e)
    {
        StartDashboardAccountsScroll(sender, e, 1);
    }

    private void OnDashboardAccountsScrollButtonReleased(object sender, MouseButtonEventArgs e)
    {
        StopDashboardAccountsScroll(sender);
        e.Handled = true;
    }

    private void OnDashboardAccountsScrollButtonLostMouseCapture(object sender, MouseEventArgs e)
    {
        StopDashboardAccountsScroll(sender);
    }

    private void OnDashboardAccountsScrollTimerTick(object? sender, EventArgs e)
    {
        if (_dashboardAccountsScrollDirection == 0)
            return;

        ScrollDashboardAccounts(_dashboardAccountsScrollDirection);
    }

    private void StartDashboardAccountsScroll(object sender, MouseButtonEventArgs e, int direction)
    {
        if (DashboardAccountsScrollViewer.ScrollableWidth <= 0)
            return;

        _dashboardAccountsScrollDirection = direction;

        if (sender is UIElement scrollButton)
            scrollButton.CaptureMouse();

        _dashboardAccountsScrollTimer.Stop();
        _dashboardAccountsScrollTimer.Start();
        e.Handled = true;
    }

    private void StopDashboardAccountsScroll(object sender)
    {
        _dashboardAccountsScrollTimer.Stop();
        _dashboardAccountsScrollDirection = 0;

        if (sender is UIElement { IsMouseCaptured: true } scrollButton)
            scrollButton.ReleaseMouseCapture();
    }

    private void ScrollDashboardAccounts(int direction)
    {
        if (DashboardAccountsScrollViewer.ScrollableWidth <= 0)
            return;

        var targetOffset = Math.Clamp(
            DashboardAccountsScrollViewer.HorizontalOffset + direction * DashboardAccountsScrollPixels,
            0,
            DashboardAccountsScrollViewer.ScrollableWidth);

        DashboardAccountsScrollViewer.ScrollToHorizontalOffset(targetOffset);
        UpdateDashboardAccountsScrollButtonVisibility();
    }

    private void UpdateDashboardAccountsScrollButtonVisibility()
    {
        if (DashboardAccountsScrollLeftButton is null ||
            DashboardAccountsScrollRightButton is null ||
            DashboardAccountsScrollViewer is null)
            return;

        var canScroll = DashboardAccountsScrollViewer.ScrollableWidth > 0;
        var canScrollLeft = canScroll &&
                            DashboardAccountsScrollViewer.HorizontalOffset > 0;
        var canScrollRight = canScroll &&
                             DashboardAccountsScrollViewer.HorizontalOffset < DashboardAccountsScrollViewer.ScrollableWidth;

        DashboardAccountsScrollLeftButton.Visibility = canScrollLeft
            ? Visibility.Visible
            : Visibility.Collapsed;
        DashboardAccountsScrollRightButton.Visibility = canScrollRight
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void OnAccountsButtonClick(object sender, RoutedEventArgs e)
    {
        _dialogService.ShowAccountsList(Window.GetWindow(this));
    }

    private void OnAddAccountButtonClick(object sender, RoutedEventArgs e)
    {
        _dialogService.ShowAddAccount(Window.GetWindow(this));
    }
}
