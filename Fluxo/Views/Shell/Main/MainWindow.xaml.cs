using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Services.Dialogs;
using Fluxo.Services.History;
using Fluxo.Services.Logging;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Popups;
using Fluxo.ViewModels.Popups.Helpers;
using Fluxo.ViewModels.Popups.Settings;
using Fluxo.ViewModels.Shell;
using Fluxo.Views.Popups;
using Microsoft.Extensions.DependencyInjection;
using DateRangeResolver = Fluxo.ViewModels.Shell.Main.DateRangeResolver;
using MainVM = Fluxo.ViewModels.Shell.Main.MainVM;

namespace Fluxo.Views.Shell.Main;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window, IPopupHost
{
    public static readonly DependencyProperty IsWindowLayoutMaximizedProperty = DependencyProperty.Register(
        nameof(IsWindowLayoutMaximized),
        typeof(bool),
        typeof(MainWindow),
        new PropertyMetadata(false));

    private enum MainDrawerPage
    {
        Analytics,
        Calendar
    }

    private const int FadeDuration = 180; // ms
    private const int StateChangeDuration = 100; // ms
    private const int AnalyticsDrawerTransitionDuration = 220; // ms
    private const int AnalyticsDrawerTabFadeDuration = 180; // ms
    private const double DashboardSpendingSourcesScrollPixels = 10;
    private const int DashboardSpendingSourcesScrollIntervalMilliseconds = 10;

    private readonly DispatcherTimer _dashboardSpendingSourcesScrollTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(DashboardSpendingSourcesScrollIntervalMilliseconds)
    };

    private readonly DispatcherTimer _headerMenuCloseTimer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private readonly DispatcherTimer _popupOverlayDeferredHideTimer = new() { Interval = TimeSpan.FromMilliseconds(FadeDuration) };
    private readonly IDataOperationRunner _dataOperationRunner;
    private readonly LogMemoryManager _logMemoryManager;
    private readonly MainVM _mainVM;
    private readonly IDialogService _dialogService;
    private readonly IServiceProvider _serviceProvider;
    private readonly PopupOverlayHandoffState _popupOverlayHandoffState = new();
    private readonly ObservableCollection<HeaderQuickSearchResult> _headerSearchResults = [];
    private Rect _currentBounds;
    private bool _hasCompletedPendingDeletionCleanup;
    private bool _hasInitializedDashboardPanels;
    private bool _isHeaderMenuPinned;
    private bool _isMaximized;
    private bool _isStateChangeTransitionActive;
    private bool _wasMinimized;
    private bool _isPointerOverHeaderMenuButton;
    private bool _isPointerOverHeaderMenuPopup;
    private bool _isAnalyticsDrawerOpen;
    private bool _isAnalyticsDrawerTransitionActive;
    private bool _isPreparingAnalyticsOpen;
    private bool _isHeaderSearchExpanded;
    private int _analyticsDrawerTabVisibilityToken;
    private EventHandler? _popupOverlayDeferredHideTickHandler;
    private bool _isAnalyticsDrawerTabVisibilityTransitionActive;
    private IServiceScope? _analyticsDrawerScope;
    private Analytics? _analyticsDrawerView;
    private MainDrawerPage? _activeDrawerPage;
    private Calendar? _calendarDrawerView;
    private IServiceScope? _calendarDrawerScope;
    private int _dashboardSpendingSourcesScrollDirection;

    private EventHandler? _renderHandler;

    public bool IsWindowLayoutMaximized
    {
        get => (bool)GetValue(IsWindowLayoutMaximizedProperty);
        private set => SetValue(IsWindowLayoutMaximizedProperty, value);
    }

    public MainWindow(
        MainVM mainVM,
        IDataOperationRunner dataOperationRunner,
        IDialogService dialogService,
        IServiceProvider serviceProvider)
    {
        InitializeComponent();

        _mainVM = mainVM;
        _dataOperationRunner = dataOperationRunner;
        _dialogService = dialogService;
        _serviceProvider = serviceProvider;
        _logMemoryManager = new LogMemoryManager(_mainVM, _dataOperationRunner);

        HeaderSearchResultsList.ItemsSource = _headerSearchResults;
        DataContext = _mainVM;
        _logMemoryManager.StateChanged += OnHistoryManagerStateChanged;
        UpdateHistoryAvailability();

        Loaded += async (_, _) =>
        {
            if (!_hasInitializedDashboardPanels)
            {
                _hasInitializedDashboardPanels = true;
                await InitializeDashboardPanelsAsync();
            }

            _currentBounds = new Rect(Left, Top, Width, Height);
            UpdateExpandRestoreButtonIcon();
            _ = Dispatcher.BeginInvoke(
                UpdateDashboardSpendingSourcesScrollButtonVisibility,
                DispatcherPriority.ApplicationIdle);
            FadeIn();
        };

        Closing += OnWindowClosing;
        Deactivated += OnWindowDeactivated;
        StateChanged += OnWindowStateChanged;
        PreviewKeyDown += OnPreviewKeyDown;
        PreviewMouseLeftButtonDown += OnWindowPreviewMouseLeftButtonDown;
        _dashboardSpendingSourcesScrollTimer.Tick += OnDashboardSpendingSourcesScrollTimerTick;
        _headerMenuCloseTimer.Tick += OnHeaderMenuCloseTimerTick;
    }

    private void MainWindow_OnMouseMove(object sender, MouseEventArgs e)
    {
        var isEligibleHeaderDrag = Mouse.LeftButton == MouseButtonState.Pressed &&
                                   e.GetPosition(this).Y < 60 &&
                                   (e.OriginalSource is not DependencyObject source || !IsInteractiveElement(source));
        var restoreMode = MainWindowDragStateDecider.DecideRestoreMode(
            _isMaximized,
            _isStateChangeTransitionActive,
            isEligibleHeaderDrag);

        if (restoreMode == MainWindowRestoreMode.Noop)
            return;

        try
        {
            if (restoreMode == MainWindowRestoreMode.InstantRestoreAndDrag)
                RestoreInstantlyForDrag();

            DragMove();
        }
        catch (Exception exception)
        {
            FluxoLogManager.LogFailureForProcess(exception, "drag the main window");
        }
    }

    // ── Fade helpers ────────────────────────────────────────────────

    private void FadeIn(Action? onCompleted = null)
    {
        var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(FadeDuration))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        if (onCompleted is not null)
            anim.Completed += (_, _) => onCompleted();

        BeginAnimation(OpacityProperty, anim);
    }

    private void FadeOut(Action onCompleted)
    {
        var anim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(FadeDuration))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        anim.Completed += (_, _) => onCompleted();
        BeginAnimation(OpacityProperty, anim);
    }

    private void FadeContentOut(Action onCompleted)
    {
        FadeElements(
            GetStateChangeFadeElements(),
            0,
            EasingMode.EaseIn,
            onCompleted);
    }

    private void FadeContentIn(Action? onCompleted = null)
    {
        FadeElements(
            GetStateChangeFadeElements(),
            1,
            EasingMode.EaseOut,
            onCompleted);
    }

    private UIElement[] GetStateChangeFadeElements()
    {
        // Avoid interrupting the tab host's own opacity animation because its completion callback re-enables the tab button.
        return _isAnalyticsDrawerTabVisibilityTransitionActive
            ? new UIElement[] { ContentGrid, AnalyticsDrawerLayer }
            : new UIElement[] { ContentGrid, AnalyticsDrawerLayer, DrawerTabHost };
    }

    private static void FadeElements(UIElement[] elements, double toOpacity, EasingMode easingMode, Action? onCompleted = null)
    {
        if (elements.Length == 0)
        {
            onCompleted?.Invoke();
            return;
        }

        var pending = elements.Length;
        foreach (var element in elements)
            FadeElement(element, toOpacity, easingMode, () =>
            {
                pending--;
                if (pending == 0)
                    onCompleted?.Invoke();
            });
    }

    private static void FadeElement(UIElement element, double toOpacity, EasingMode easingMode, Action? onCompleted = null)
    {
        element.BeginAnimation(OpacityProperty, null);

        var animation = new DoubleAnimation(element.Opacity, toOpacity, TimeSpan.FromMilliseconds(FadeDuration))
        {
            EasingFunction = new CubicEase { EasingMode = easingMode }
        };

        if (onCompleted is not null)
            animation.Completed += (_, _) => onCompleted();

        element.BeginAnimation(OpacityProperty, animation);
    }

    // ── SystemCommand handlers ───────────────────────────────────────

    private void OnCloseWindow(object sender, ExecutedRoutedEventArgs e)
    {
        FadeOut(() => SystemCommands.CloseWindow(this));
    }

    private async void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_hasCompletedPendingDeletionCleanup)
            return;

        if (Application.Current is App app && await app.TryHandleMainWindowClosingToTrayAsync(this))
        {
            e.Cancel = true;
            return;
        }

        if (!App.TryCloseOwnedWindows(this))
        {
            e.Cancel = true;
            return;
        }

        e.Cancel = true;

        try
        {
            _hasCompletedPendingDeletionCleanup = true;
            _logMemoryManager.StateChanged -= OnHistoryManagerStateChanged;
            _logMemoryManager.Dispose();
        }
        finally
        {
            CancelPendingPopupOverlayDeferredHide();
            DisposeAnalyticsDrawer();
            Application.Current.Shutdown(0);
        }
    }

    private void OnMinimizeWindow(object sender, ExecutedRoutedEventArgs e)
    {
        FadeOut(() => SystemCommands.MinimizeWindow(this));
    }

    public void HideToTray()
    {
        CancelPendingPopupOverlayDeferredHide();
        CloseHeaderMenu();
        CollapseHeaderSearch();

        // If close-to-tray happens after a fade-out close animation, the window can
        // remain at zero opacity. Normalize before hiding so next restore is visible.
        BeginAnimation(OpacityProperty, null);
        Opacity = 1;

        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;

        ShowInTaskbar = false;
        Hide();
    }

    public void ShowFromTray()
    {
        // Always clear any previous opacity animation/state before showing.
        BeginAnimation(OpacityProperty, null);
        Opacity = 0;
        ShowInTaskbar = true;

        if (!IsVisible)
            Show();

        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;

        // Force foreground activation from tray restores. A simple Activate/Focus
        // is not always enough after interacting with a tray popup.
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
        Keyboard.Focus(this);

        Dispatcher.BeginInvoke(() =>
        {
            Activate();
            Focus();
            Keyboard.Focus(this);
        }, DispatcherPriority.ApplicationIdle);

        FadeIn();
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            _wasMinimized = true;
            BeginAnimation(OpacityProperty, null);
            Opacity = 0;
        }
        else if (WindowState == WindowState.Normal && _wasMinimized)
        {
            _wasMinimized = false;
            FadeIn();
        }
    }

    private void OnExpandRestoreWindow(object sender, RoutedEventArgs e)
    {
        if (_isMaximized)
            AnimateToRestored();
        else
            AnimateToMaximized();
    }

    private void UpdateExpandRestoreButtonIcon()
    {
        if (ExpandRestoreButton is null)
            return;

        var iconKey = _isMaximized ? "CompressAlt" : "ExpandAlt";
        ExpandRestoreButton.ButtonIcon = FindResource(iconKey);
    }

    private void AnimateToMaximized()
    {
        if (_isMaximized || _isStateChangeTransitionActive)
            return;

        var from = new Rect(Left, Top, Width, Height);
        _isMaximized = true;
        UpdateExpandRestoreButtonIcon();

        var workArea = GetMonitorWorkArea();
        _currentBounds = workArea;
        AnimateStateChange(from, workArea, true);
    }

    private void AnimateToRestored()
    {
        if (!_isMaximized || _isStateChangeTransitionActive)
            return;

        var from = new Rect(Left, Top, Width, Height);
        _isMaximized = false;
        UpdateExpandRestoreButtonIcon();

        var workArea = GetMonitorWorkArea();
        var restoreBounds = WindowRestoreBoundsResolver.ResolveCenteredRestoreBounds(workArea);
        _currentBounds = restoreBounds;
        AnimateStateChange(from, restoreBounds, false);
    }

    private void RestoreInstantlyForDrag()
    {
        ClearWindowBoundsAnimations();

        var preRestorePointer = Mouse.GetPosition(this);
        var preRestoreWidth = Width;
        var preRestoreHeight = Height;

        var workArea = GetMonitorWorkArea();
        var restoreBounds = WindowRestoreBoundsResolver.ResolveCenteredRestoreBounds(workArea);
        var anchoredRestoreBounds = ResolvePointerAnchoredRestoreBounds(
            restoreBounds,
            workArea,
            preRestorePointer,
            preRestoreWidth,
            preRestoreHeight);

        RootBorder.CornerRadius = new CornerRadius(16);
        RootBorder.BorderThickness = new Thickness(1);

        Left = anchoredRestoreBounds.Left;
        Top = anchoredRestoreBounds.Top;
        Width = anchoredRestoreBounds.Width;
        Height = anchoredRestoreBounds.Height;

        _isMaximized = false;
        IsWindowLayoutMaximized = false;
        _isStateChangeTransitionActive = false;
        _currentBounds = anchoredRestoreBounds;
        UpdateExpandRestoreButtonIcon();
    }

    private Rect ResolvePointerAnchoredRestoreBounds(
        Rect restoreBounds,
        Rect workArea,
        Point preRestorePointer,
        double preRestoreWidth,
        double preRestoreHeight)
    {
        var cursorScreenDevice = PointToScreen(preRestorePointer);
        var source = PresentationSource.FromVisual(this);
        var fromDevice = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var cursorScreenDip = fromDevice.Transform(cursorScreenDevice);

        var relativeX = preRestoreWidth <= 0 ? 0 : Math.Clamp(preRestorePointer.X / preRestoreWidth, 0, 1);
        var relativeY = preRestoreHeight <= 0 ? 0 : Math.Clamp(preRestorePointer.Y / preRestoreHeight, 0, 1);

        var targetLeft = cursorScreenDip.X - (restoreBounds.Width * relativeX);
        var targetTop = cursorScreenDip.Y - (restoreBounds.Height * relativeY);

        return new Rect(
            ClampWindowOrigin(targetLeft, workArea.Left, workArea.Right - restoreBounds.Width),
            ClampWindowOrigin(targetTop, workArea.Top, workArea.Bottom - restoreBounds.Height),
            restoreBounds.Width,
            restoreBounds.Height);
    }

    private static double ClampWindowOrigin(double value, double min, double max)
    {
        if (max < min)
            return min;

        return Math.Clamp(value, min, max);
    }

    private void AnimateStateChange(Rect from, Rect to, bool maximizing)
    {
        _isStateChangeTransitionActive = true;

        FadeContentOut(() =>
        {
            RootBorder.CornerRadius = maximizing ? new CornerRadius(0) : new CornerRadius(16);
            RootBorder.BorderThickness = maximizing ? new Thickness(0) : new Thickness(1);

            AnimateBounds(from, to, maximizing, () =>
            {
                IsWindowLayoutMaximized = maximizing;
                FadeContentIn(() =>
                {
                    _isStateChangeTransitionActive = false;
                });
            });
        });
    }

    private void AnimateBounds(Rect from, Rect to, bool maximizing, Action onCompleted)
    {
        ClearWindowBoundsAnimations();

        var ease = new CubicEase
        {
            EasingMode = maximizing ? EasingMode.EaseOut : EasingMode.EaseInOut
        };
        var durationMs = (double)StateChangeDuration;
        var startTime = TimeSpan.Zero;

        _renderHandler = (sender, e) =>
        {
            var timestamp = ((RenderingEventArgs)e).RenderingTime;
            if (startTime == TimeSpan.Zero)
                startTime = timestamp;

            var t = Math.Min(1.0, (timestamp - startTime).TotalMilliseconds / durationMs);
            var eased = ease.Ease(t);

            var currentBounds = WindowBoundsInterpolator.Interpolate(from, to, eased);
            Left = currentBounds.Left;
            Top = currentBounds.Top;
            Width = currentBounds.Width;
            Height = currentBounds.Height;

            if (t >= 1.0)
            {
                Left = to.Left;
                Top = to.Top;
                Width = to.Width;
                Height = to.Height;
                CompositionTarget.Rendering -= _renderHandler;
                _renderHandler = null;
                onCompleted();
            }
        };

        CompositionTarget.Rendering += _renderHandler;
    }

    private void ClearWindowBoundsAnimations()
    {
        if (_renderHandler is not null)
        {
            CompositionTarget.Rendering -= _renderHandler;
            _renderHandler = null;
        }

        BeginAnimation(LeftProperty, null);
        BeginAnimation(TopProperty, null);
        BeginAnimation(WidthProperty, null);
        BeginAnimation(HeightProperty, null);
    }

    // ── Monitor work area ───────────────────────────────────────────

    private Rect GetMonitorWorkArea()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var monitor = MonitorFromWindow(hwnd, 2 /* MONITOR_DEFAULTTONEAREST */);
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        GetMonitorInfo(monitor, ref info);

        var source = PresentationSource.FromVisual(this);
        var fromDevice = source?.CompositionTarget?.TransformFromDevice
                         ?? Matrix.Identity;

        return new Rect(
            info.rcWork.Left * fromDevice.M11,
            info.rcWork.Top * fromDevice.M22,
            (info.rcWork.Right - info.rcWork.Left) * fromDevice.M11,
            (info.rcWork.Bottom - info.rcWork.Top) * fromDevice.M22);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    // ── Shared UI helpers ───────────────────────────────────────────

    private async Task InitializeDashboardPanelsAsync()
    {
        try
        {
            await _mainVM.Initialize();
        }
        catch (Exception exception)
        {
            FluxoLogManager.LogError(exception, "Unable to initialize dashboard panels.");
            _dialogService.ShowError(
                FluxoLogManager.CreateFailureMessage("initialize dashboard panels"),
                "Dashboard",
                this);
        }
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

    private void OnTopBorderHitAreaMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 || !IsActive)
            return;

        if (HeaderMenuPopup.IsOpen)
            return;

        if (_isMaximized)
            AnimateToRestored();
        else
            AnimateToMaximized();

        e.Handled = true;
    }

    private static bool IsInteractiveElement(DependencyObject source)
    {
        for (var current = source; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is ScrollBar or Thumb or ButtonBase or ListViewItem or TextBoxBase or Selector)
                return true;

        return false;
    }

    private static T? FindAncestor<T>(DependencyObject source) where T : DependencyObject
    {
        for (var current = source; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is T match)
                return match;

        return null;
    }

    private static bool IsDescendantOf(DependencyObject source, DependencyObject ancestor)
    {
        for (var current = source; current is not null; current = VisualTreeHelper.GetParent(current))
            if (ReferenceEquals(current, ancestor))
                return true;

        return false;
    }

    // ── Keyboard shortcuts ────────────────────────────────────────────

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_isHeaderSearchExpanded && e.Key == Key.Escape)
        {
            CollapseHeaderSearch();
            e.Handled = true;
            return;
        }

        if (_isAnalyticsDrawerOpen && MainWindowShortcutMatcher.IsCloseAnalyticsShortcut(e.Key, Keyboard.Modifiers))
        {
            CloseAnalyticsDrawer();
            e.Handled = true;
            return;
        }

        if (MainWindowShortcutMatcher.IsOpenQuickAddShortcut(e.Key, Keyboard.Modifiers))
        {
            if (IsSufficientFundsActionGateLocked())
            {
                e.Handled = true;
                return;
            }

            OpenQuickAddPopup();
            e.Handled = true;
            return;
        }

        if (MainWindowShortcutMatcher.IsOpenSearchShortcut(e.Key, Keyboard.Modifiers))
        {
            if (IsSufficientFundsActionGateLocked())
            {
                e.Handled = true;
                return;
            }

            ExpandHeaderSearch();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
        {
            OpenSettingsPopup();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.L && Keyboard.Modifiers == ModifierKeys.Control)
        {
            OpenSpendingSourcesListPopup();
            e.Handled = true;
            return;
        }

        if (MainWindowShortcutMatcher.IsRunSetupWizardShortcut(e.Key, Keyboard.Modifiers))
        {
            RunSetupWizardFromShortcutAsync();
            e.Handled = true;
            return;
        }

        if (MainWindowShortcutMatcher.IsOpenPlanningShortcut(e.Key, Keyboard.Modifiers))
        {
            if (IsSufficientFundsActionGateLocked())
            {
                e.Handled = true;
                return;
            }

            OpenPlanningPopup();
            e.Handled = true;
            return;
        }

        if (MainWindowShortcutMatcher.IsOpenAnalyticsShortcut(e.Key, Keyboard.Modifiers))
        {
            if (IsSufficientFundsActionGateLocked())
            {
                e.Handled = true;
                return;
            }

            _ = OpenAnalyticsPopupAsync();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z && !IsTextInputElementFocused() &&
            _logMemoryManager.CanUndo)
        {
            if (IsSufficientFundsActionGateLocked())
            {
                e.Handled = true;
                return;
            }

            _ = UndoLogMemoryAsync();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Y && !IsTextInputElementFocused() &&
            _logMemoryManager.CanRedo)
        {
            if (IsSufficientFundsActionGateLocked())
            {
                e.Handled = true;
                return;
            }

            _ = RedoLogMemoryAsync();
            e.Handled = true;
        }
    }

    private async void RunSetupWizardFromShortcutAsync()
    {
        if (FluxoMessageBox.Show(this,
                "This will close the current window and open the setup wizard. Continue?",
                "Run Setup Wizard",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        await ((App)Application.Current).RunSetupWizardAsync();
    }

    private void OnHeaderSearchButtonClick(object sender, RoutedEventArgs e)
    {
        if (IsSufficientFundsActionGateLocked())
            return;

        ExpandHeaderSearch();
    }

    private void OnHeaderQuickAddButtonClick(object sender, RoutedEventArgs e)
    {
        if (IsSufficientFundsActionGateLocked())
            return;

        OpenAddNewTransactionPopup();
    }

    private void OnHeaderSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateHeaderSearchResults();
    }

    private void OnHeaderSearchBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;

        CollapseHeaderSearch();
        e.Handled = true;
    }

    private void OnHeaderSearchRegionPreviewLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!_isHeaderSearchExpanded || e.NewFocus is not DependencyObject newFocus)
            return;

        if (IsDescendantOf(newFocus, HeaderSearchRegion))
            return;

        CollapseHeaderSearch();
    }

    private void OnHeaderSearchResultItemClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: HeaderQuickSearchResult result })
            return;

        CollapseHeaderSearch();

        if (result.ExpenseLog is not null)
            OpenExpenseDetailPopup(result.ExpenseLog);
        else if (result.IncomeLog is not null)
            OpenIncomeDetailPopup(result.IncomeLog);

        e.Handled = true;
    }

    private void ExpandHeaderSearch()
    {
        if (!_isHeaderSearchExpanded)
        {
            _isHeaderSearchExpanded = true;
            HeaderSearchButton.Visibility = Visibility.Collapsed;
            HeaderSearchInputBorder.Visibility = Visibility.Visible;
        }

        HeaderSearchBox.Focus();
        HeaderSearchBox.SelectAll();
        UpdateHeaderSearchResults();
    }

    private void CollapseHeaderSearch()
    {
        if (!_isHeaderSearchExpanded)
            return;

        _isHeaderSearchExpanded = false;
        HeaderSearchButton.Visibility = Visibility.Visible;
        HeaderSearchInputBorder.Visibility = Visibility.Collapsed;
        HeaderSearchResultsPopup.IsOpen = false;
        HeaderSearchNoResultsText.Visibility = Visibility.Collapsed;
        HeaderSearchBox.Text = string.Empty;
        _headerSearchResults.Clear();
    }

    private void UpdateHeaderSearchResults()
    {
        if (!_isHeaderSearchExpanded)
            return;

        var query = HeaderSearchBox.Text;
        var matches = HeaderQuickSearchEngine.Search(
            _mainVM.BudgetPanel.GetAllExpenseLogs(),
            _mainVM.BudgetPanel.GetAllIncomeLogs(),
            query).ToList();

        _headerSearchResults.Clear();

        if (matches.Count == 0)
        {
            var normalizedQuery = query?.Trim();
            if (string.IsNullOrEmpty(normalizedQuery) || normalizedQuery.Length <= 3)
            {
                HeaderSearchResultsPopup.IsOpen = false;
                HeaderSearchNoResultsText.Visibility = Visibility.Collapsed;
                return;
            }

            HeaderSearchResultsPopup.IsOpen = true;
            HeaderSearchNoResultsText.Visibility = Visibility.Visible;
            return;
        }

        HeaderSearchResultsPopup.IsOpen = true;
        HeaderSearchNoResultsText.Visibility = Visibility.Collapsed;
        foreach (var match in matches)
            _headerSearchResults.Add(match);
    }

    private void OnQuickAddButtonClick(object sender, RoutedEventArgs e)
    {
        CloseHeaderMenu();
        if (IsSufficientFundsActionGateLocked())
            return;

        OpenQuickAddPopup();
    }

    private void OnHeaderMenuButtonMouseEnter(object sender, MouseEventArgs e)
    {
        _isPointerOverHeaderMenuButton = true;
        _headerMenuCloseTimer.Stop();
        OpenHeaderMenu(false);
    }

    private void OnHeaderMenuButtonMouseLeave(object sender, MouseEventArgs e)
    {
        _isPointerOverHeaderMenuButton = false;
        ScheduleHeaderMenuClose();
    }

    private void OnHeaderMenuPopupMouseEnter(object sender, MouseEventArgs e)
    {
        _isPointerOverHeaderMenuPopup = true;
        _headerMenuCloseTimer.Stop();
    }

    private void OnHeaderMenuPopupMouseLeave(object sender, MouseEventArgs e)
    {
        _isPointerOverHeaderMenuPopup = false;
        ScheduleHeaderMenuClose();
    }

    private void OnHeaderMenuButtonClick(object sender, RoutedEventArgs e)
    {
        OpenHeaderMenu(true);
    }

    private void OnSpendingSourcesButtonClick(object sender, RoutedEventArgs e)
    {
        CloseHeaderMenu();
        OpenSpendingSourcesListPopup();
    }

    private async void OnUndoButtonClick(object sender, RoutedEventArgs e)
    {
        CloseHeaderMenu();
        if (IsSufficientFundsActionGateLocked())
            return;

        await UndoLogMemoryAsync();
    }

    private async void OnRedoButtonClick(object sender, RoutedEventArgs e)
    {
        CloseHeaderMenu();
        if (IsSufficientFundsActionGateLocked())
            return;

        await RedoLogMemoryAsync();
    }

    private void OnSettingsButtonClick(object sender, RoutedEventArgs e)
    {
        CloseHeaderMenu();
        OpenSettingsPopup();
    }

    private void OnPlanningButtonClick(object sender, RoutedEventArgs e)
    {
        CloseHeaderMenu();
        if (IsSufficientFundsActionGateLocked())
            return;

        OpenPlanningPopup();
    }

    private async void OnAnalyticsDrawerTabClick(object sender, RoutedEventArgs e)
    {
        if (IsSufficientFundsActionGateLocked())
            return;

        CloseHeaderMenu();

        if (_isAnalyticsDrawerOpen && _activeDrawerPage == MainDrawerPage.Analytics)
        {
            CloseAnalyticsDrawer();
            return;
        }

        await OpenDrawerPageAsync(MainDrawerPage.Analytics);
    }

    private async void OnCalendarDrawerTabClick(object sender, RoutedEventArgs e)
    {
        if (IsSufficientFundsActionGateLocked())
            return;

        CloseHeaderMenu();

        if (_isAnalyticsDrawerOpen && _activeDrawerPage == MainDrawerPage.Calendar)
        {
            CloseAnalyticsDrawer();
            return;
        }

        await OpenDrawerPageAsync(MainDrawerPage.Calendar);
    }

    private void OnCloseAnalyticsDrawerButtonClick(object sender, RoutedEventArgs e)
    {
        CloseAnalyticsDrawer();
    }

    private void OnAddSpendingSourceButtonClick(object sender, RoutedEventArgs e)
    {
        OpenAddSpendingSourcePopup();
    }

    private void OnDashboardSpendingAmountGateActionClick(object sender, RoutedEventArgs e)
    {
        OpenAddSpendingSourcePopup();
    }

    public void OpenQuickAddPopup(QuickAddVM.QuickAddDraft? draft = null)
    {
        if (draft is { } popupDraft)
        {
            if (IsSufficientFundsActionGateLocked())
                return;

            OpenAddNewTransactionPopup(popupDraft);
            return;
        }

        if (IsSufficientFundsActionGateLocked())
            return;

        _dialogService.ShowQuickAdd(this);
    }

    public void OpenAddNewTransactionPopupForCategory(ExpenseCategory category)
    {
        OpenAddNewTransactionPopup(new QuickAddVM.QuickAddDraft(
            true,
            string.Empty,
            0m,
            null,
            DateTime.Today,
            string.Empty,
            category,
            null));
    }

    public void OpenAddNewTransactionPopup(QuickAddVM.QuickAddDraft? draft = null)
    {
        if (IsSufficientFundsActionGateLocked())
            return;

        using var scope = _serviceProvider.CreateScope();
        var appData = scope.ServiceProvider.GetRequiredService<IAppDataService>();
        var popupViewModel = new QuickAddVM(_mainVM, appData);
        if (draft is { } popupDraft)
            popupViewModel.InitializeFromDraft(popupDraft);

        _dialogService.ShowAddNewTransaction(popupViewModel, this);
    }

    public void OpenExpenseDetailPopup(ExpenseLogVM expenseLog)
    {
        using var scope = _serviceProvider.CreateScope();
        var appData = scope.ServiceProvider.GetRequiredService<IAppDataService>();
        var popupViewModel = new ExpenseDetailVM(_mainVM, expenseLog, appData);
        _dialogService.ShowExpenseDetail(popupViewModel, this);
    }

    public void OpenIncomeDetailPopup(IncomeLogVM incomeLog)
    {
        using var scope = _serviceProvider.CreateScope();
        var appData = scope.ServiceProvider.GetRequiredService<IAppDataService>();
        var popupViewModel = new IncomeDetailVM(_mainVM, incomeLog, appData);
        _dialogService.ShowIncomeDetail(popupViewModel, this);
    }

    public void OpenSpendingSourcesListPopup()
    {
        _dialogService.ShowSpendingSourcesList(this);
    }

    public void OpenAddSpendingSourcePopup()
    {
        _dialogService.ShowAddSpendingSource(this);
    }

    public void OpenAddSavingGoalPopup()
    {
        if (IsSufficientFundsActionGateLocked())
            return;

        _dialogService.ShowAddSavingGoal(this);
    }

    public void OpenSettingsPopup()
    {
        _dialogService.ShowSettings(this);
    }

    public void OpenQuickSetupWizardPopup()
    {
        _dialogService.ShowQuickSetupWizard(this);
    }

    public void OpenPlanningPopup()
    {
        if (IsSufficientFundsActionGateLocked())
            return;

        _dialogService.ShowPlanningPopup(this);
    }

    public void OpenAnalyticsPopup()
    {
        if (IsSufficientFundsActionGateLocked())
            return;

        _ = OpenAnalyticsPopupAsync();
    }

    private async Task OpenAnalyticsPopupAsync()
    {
        await OpenDrawerPageAsync(MainDrawerPage.Analytics);
    }

    private async Task OpenDrawerPageAsync(MainDrawerPage page)
    {
        if (IsSufficientFundsActionGateLocked())
            return;

        if (_isAnalyticsDrawerTransitionActive || _isPreparingAnalyticsOpen)
            return;

        _isPreparingAnalyticsOpen = true;
        SetDrawerTabButtonsEnabled(false);

        try
        {
            SetDrawerTitle(page == MainDrawerPage.Analytics ? "Analytics" : "Calendar");
            SetAnalyticsDateRangeSelectorVisibility(page);

            if (page == MainDrawerPage.Analytics)
            {
                EnsureAnalyticsDrawerLoaded();
                ApplyMainWindowRangeToAnalyticsIfBounded();

                if (_analyticsDrawerView is null)
                    return;

                await _dialogService.ShowToastWhileAsync(
                    "Loading analytics",
                    () => _analyticsDrawerView.PrepareForOpenAsync(showInternalToast: false),
                    this);
            }
            else
            {
                EnsureCalendarDrawerLoaded();

                if (_calendarDrawerView is null)
                    return;

                await _dialogService.ShowToastWhileAsync(
                    "Loading calendar",
                    () => _calendarDrawerView.PrepareForOpenAsync(),
                    this);
            }

            _activeDrawerPage = page;
            OpenAnalyticsDrawer();
        }
        catch (Exception exception)
        {
            var label = page == MainDrawerPage.Analytics ? "analytics" : "calendar";
            FluxoLogManager.LogError(exception, $"Unable to open {label} drawer.");
            _dialogService.ShowError(
                FluxoLogManager.CreateFailureMessage($"open {label}"),
                page == MainDrawerPage.Analytics ? "Analytics" : "Calendar",
                this);
        }
        finally
        {
            _isPreparingAnalyticsOpen = false;

            if (!_isAnalyticsDrawerOpen && !_isAnalyticsDrawerTransitionActive)
                SetDrawerTabButtonsEnabled(true);
        }
    }

    public void OpenSpendingSourceDetailPopup(SpendingSourceVM spendingSource)
    {
        using var scope = _serviceProvider.CreateScope();
        var appData = scope.ServiceProvider.GetRequiredService<IAppDataService>();
        var popupViewModel = new SpendingSourceDetailVM(_mainVM, spendingSource.Id, appData);
        _dialogService.ShowSpendingSourceDetail(popupViewModel, this);
    }

    public void ToggleSpendingSourceFilter(SpendingSourceVM? spendingSource)
    {
        _mainVM.ToggleSpendingSourceFilter(spendingSource);
    }

    public async Task ExecuteDeleteSpendingSourceActionAsync(SpendingSourceVM spendingSource)
    {
        await ExecuteSpendingSourceSettingsActionAsync(spendingSource, SettingsBatchAction.Delete, true);
    }

    public async Task ExecuteHideSpendingSourceActionAsync(SpendingSourceVM spendingSource)
    {
        await ExecuteSpendingSourceSettingsActionAsync(spendingSource, SettingsBatchAction.Hide);
    }

    public async Task ExecuteDisableSpendingSourceActionAsync(SpendingSourceVM spendingSource)
    {
        await ExecuteSpendingSourceSettingsActionAsync(spendingSource, SettingsBatchAction.Disable);
    }

    public void OpenTransferFundsPopup(SpendingSourceVM spendingSource)
    {
        ArgumentNullException.ThrowIfNull(spendingSource);

        if (!spendingSource.CanTransfer)
            return;

        using var scope = _serviceProvider.CreateScope();
        var appData = scope.ServiceProvider.GetRequiredService<IAppDataService>();
        var transferVm = new TransferFundsVM(_mainVM, spendingSource, appData);
        _dialogService.ShowTransferFunds(transferVm, this);
    }

    private void EnsureAnalyticsDrawerLoaded()
    {
        if (_analyticsDrawerView is null)
        {
            _analyticsDrawerScope = _serviceProvider.CreateScope();
            _analyticsDrawerView = _analyticsDrawerScope.ServiceProvider.GetRequiredService<Analytics>();
        }

        AnalyticsDrawerContentHost.Content = _analyticsDrawerView;
        AnalyticsDateRangeSelectorHost.DataContext = _analyticsDrawerView.DataContext;
    }

    private void EnsureCalendarDrawerLoaded()
    {
        if (_calendarDrawerView is not null)
        {
            AnalyticsDrawerContentHost.Content = _calendarDrawerView;
            return;
        }

        _calendarDrawerScope = _serviceProvider.CreateScope();
        _calendarDrawerView = _calendarDrawerScope.ServiceProvider.GetRequiredService<Calendar>();
        AnalyticsDrawerContentHost.Content = _calendarDrawerView;
    }

    private void SetDrawerTitle(string title)
    {
        AnalyticsDrawerTitle.Text = title;
    }

    private void SetAnalyticsDateRangeSelectorVisibility(MainDrawerPage page)
    {
        AnalyticsDateRangeSelectorHost.Visibility = page switch
        {
            MainDrawerPage.Analytics => Visibility.Visible,
            MainDrawerPage.Calendar => Visibility.Collapsed,
            _ => Visibility.Collapsed
        };
    }

    private void SetDrawerTabButtonsEnabled(bool isEnabled)
    {
        AnalyticsDrawerTabButton.IsEnabled = isEnabled;
        CalendarDrawerTabButton.IsEnabled = isEnabled;
    }

    private void ApplyMainWindowRangeToAnalyticsIfBounded()
    {
        if (_analyticsDrawerView is null)
            return;

        var selectedMode = _mainVM.ViewModeToggle.SelectedMainContentViewMode;
        if (selectedMode == MainContentViewMode.AllTime)
            return;

        var selectedDate = _mainVM.DaySpinner.SelectedDay.Date;
        if (selectedDate == default)
            selectedDate = DateTime.Today;

        var range = DateRangeResolver.Resolve(selectedDate, selectedMode);
        _analyticsDrawerView.ApplyOpenRange(range.From, range.To);
    }

    private void OpenAnalyticsDrawer()
    {
        if (_isAnalyticsDrawerOpen || _isAnalyticsDrawerTransitionActive)
            return;

        AnalyticsDrawerPanel.Visibility = Visibility.Visible;
        AnalyticsDrawerPanel.IsHitTestVisible = true;
        AnalyticsDrawerPanel.UpdateLayout();
        SetAnalyticsDrawerTabVisibility(visible: false, animate: true);
        AnimateAnalyticsDrawer(opening: true);
    }

    private void CloseAnalyticsDrawer()
    {
        if (!_isAnalyticsDrawerOpen || _isAnalyticsDrawerTransitionActive)
            return;

        AnimateAnalyticsDrawer(opening: false);
    }

    private void AnimateAnalyticsDrawer(bool opening)
    {
        _isAnalyticsDrawerTransitionActive = true;
        SetDrawerTabButtonsEnabled(false);

        AnalyticsDrawerTransform.BeginAnimation(TranslateTransform.YProperty, null);

        var panelHeight = Math.Max(AnalyticsDrawerPanel.ActualHeight, 1);
        var from = opening ? panelHeight : 0;
        var to = opening ? 0 : panelHeight;
        var shouldReduceMotion = ShouldReduceMotion();

        if (shouldReduceMotion)
        {
            _isAnalyticsDrawerOpen = opening;
            _isAnalyticsDrawerTransitionActive = false;

            if (opening)
            {
                AnalyticsDrawerTransform.Y = 0;
                AnalyticsDrawerPanel.Visibility = Visibility.Visible;
                AnalyticsDrawerPanel.IsHitTestVisible = true;
                SetDrawerTabButtonsEnabled(false);
                FocusAnalyticsDrawerForShortcuts();
            }
            else
            {
                AnalyticsDrawerPanel.Visibility = Visibility.Collapsed;
                AnalyticsDrawerPanel.IsHitTestVisible = false;
                AnalyticsDrawerTransform.Y = panelHeight;
                SetAnalyticsDrawerTabVisibility(visible: true, animate: false, onCompleted: () =>
                {
                    SetDrawerTabButtonsEnabled(true);
                });
            }

            return;
        }

        var animation = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(AnalyticsDrawerTransitionDuration))
        {
            EasingFunction = new CubicEase { EasingMode = opening ? EasingMode.EaseOut : EasingMode.EaseIn }
        };

        animation.Completed += (_, _) =>
        {
            _isAnalyticsDrawerOpen = opening;
            _isAnalyticsDrawerTransitionActive = false;

            if (!opening)
            {
                AnalyticsDrawerPanel.Visibility = Visibility.Collapsed;
                AnalyticsDrawerPanel.IsHitTestVisible = false;
                AnalyticsDrawerTransform.BeginAnimation(TranslateTransform.YProperty, null);
                AnalyticsDrawerTransform.Y = panelHeight;
                SetAnalyticsDrawerTabVisibility(visible: true, animate: true, onCompleted: () =>
                {
                    SetDrawerTabButtonsEnabled(true);
                });
            }
            else
            {
                SetDrawerTabButtonsEnabled(false);
                FocusAnalyticsDrawerForShortcuts();
            }
        };

        AnalyticsDrawerTransform.BeginAnimation(TranslateTransform.YProperty, animation);
    }

    private void FocusAnalyticsDrawerForShortcuts()
    {
        AnalyticsDrawerPanel.Focus();
        Keyboard.Focus(AnalyticsDrawerPanel);
    }

    private void SetAnalyticsDrawerTabVisibility(bool visible, bool animate, Action? onCompleted = null)
    {
        if (ShouldReduceMotion())
            animate = false;

        _analyticsDrawerTabVisibilityToken++;
        var visibilityToken = _analyticsDrawerTabVisibilityToken;
        _isAnalyticsDrawerTabVisibilityTransitionActive = false;

        DrawerTabHost.BeginAnimation(OpacityProperty, null);

        if (!animate)
        {
            DrawerTabHost.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            DrawerTabHost.IsHitTestVisible = visible;
            DrawerTabHost.Opacity = visible ? 1d : 0d;
            onCompleted?.Invoke();
            return;
        }

        if (visible)
        {
            DrawerTabHost.Visibility = Visibility.Visible;
            DrawerTabHost.IsHitTestVisible = true;
        }
        else
        {
            DrawerTabHost.IsHitTestVisible = false;
        }

        var fromOpacity = visible ? DrawerTabHost.Opacity : 1d;
        var toOpacity = visible ? 1d : 0d;

        if (Math.Abs(fromOpacity - toOpacity) < 0.001d)
        {
            if (!visible)
                DrawerTabHost.Visibility = Visibility.Collapsed;

            onCompleted?.Invoke();
            return;
        }

        _isAnalyticsDrawerTabVisibilityTransitionActive = true;
        var tabAnimation = new DoubleAnimation(fromOpacity, toOpacity, TimeSpan.FromMilliseconds(AnalyticsDrawerTabFadeDuration))
        {
            EasingFunction = new CubicEase { EasingMode = visible ? EasingMode.EaseOut : EasingMode.EaseIn }
        };

        tabAnimation.Completed += (_, _) =>
        {
            if (visibilityToken != _analyticsDrawerTabVisibilityToken)
                return;

            _isAnalyticsDrawerTabVisibilityTransitionActive = false;

            if (!visible)
                DrawerTabHost.Visibility = Visibility.Collapsed;

            onCompleted?.Invoke();
        };

        DrawerTabHost.BeginAnimation(OpacityProperty, tabAnimation);
    }

    private static bool ShouldReduceMotion()
    {
        return !SystemParameters.ClientAreaAnimation;
    }

    private void DisposeAnalyticsDrawer()
    {
        AnalyticsDrawerContentHost.Content = null;
        _analyticsDrawerView = null;
        _analyticsDrawerScope?.Dispose();
        _analyticsDrawerScope = null;
        _calendarDrawerView = null;
        _calendarDrawerScope?.Dispose();
        _calendarDrawerScope = null;
        _activeDrawerPage = null;
        _isAnalyticsDrawerOpen = false;
        _isAnalyticsDrawerTransitionActive = false;
        SetAnalyticsDrawerTabVisibility(visible: true, animate: false);
        SetDrawerTabButtonsEnabled(true);
    }

    // ── Popup overlay & blur ────────────────────────────────────────

    public void BeginPopupHandoff()
    {
        // Compatibility marker for hosts that still announce a handoff before closing.
    }

    public void ShowPopupOverlay()
    {
        CancelPendingPopupOverlayDeferredHide();

        var hostAction = _popupOverlayHandoffState.OnPopupShown();
        if (hostAction != PopupOverlayHostAction.ShowOverlay)
            return;

        ApplyPopupBlur();
        PopupOverlay.BeginAnimation(OpacityProperty, null);
        PopupOverlay.Visibility = Visibility.Visible;

        var fadeIn = new DoubleAnimation(PopupOverlay.Opacity, 0.5, TimeSpan.FromMilliseconds(FadeDuration))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        PopupOverlay.BeginAnimation(OpacityProperty, fadeIn);
    }

    public void HidePopupOverlay()
    {
        var hostAction = _popupOverlayHandoffState.OnPopupHidden();
        if (hostAction != PopupOverlayHostAction.HideOverlay)
            return;

        HidePopupOverlayCore();
    }

    public void HidePopupOverlayForHandoff()
    {
        var hostAction = _popupOverlayHandoffState.OnPopupHiddenForHandoff(out var deferredHideGeneration);
        if (hostAction == PopupOverlayHostAction.HideOverlay)
        {
            HidePopupOverlayCore();
            return;
        }

        if (hostAction != PopupOverlayHostAction.DeferHide)
            return;

        SchedulePopupOverlayDeferredHide(deferredHideGeneration);
    }

    private void SchedulePopupOverlayDeferredHide(int deferredHideGeneration)
    {
        CancelPendingPopupOverlayDeferredHide();
        _popupOverlayDeferredHideTickHandler = (_, _) => OnPopupOverlayDeferredHideTimerTick(deferredHideGeneration);
        _popupOverlayDeferredHideTimer.Tick += _popupOverlayDeferredHideTickHandler;
        _popupOverlayDeferredHideTimer.Start();
    }

    private void OnPopupOverlayDeferredHideTimerTick(int deferredHideGeneration)
    {
        CancelPendingPopupOverlayDeferredHide();

        var hostAction = _popupOverlayHandoffState.ResolveDeferredHide(deferredHideGeneration);
        if (hostAction != PopupOverlayHostAction.HideOverlay)
            return;

        HidePopupOverlayCore();
    }

    private void CancelPendingPopupOverlayDeferredHide()
    {
        _popupOverlayDeferredHideTimer.Stop();
        if (_popupOverlayDeferredHideTickHandler is null)
            return;

        _popupOverlayDeferredHideTimer.Tick -= _popupOverlayDeferredHideTickHandler;
        _popupOverlayDeferredHideTickHandler = null;
    }

    private void HidePopupOverlayCore()
    {
        PopupOverlay.BeginAnimation(OpacityProperty, null);

        var fadeOut = new DoubleAnimation(PopupOverlay.Opacity, 0, TimeSpan.FromMilliseconds(FadeDuration))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        fadeOut.Completed += (_, _) =>
        {
            if (_popupOverlayHandoffState.ActivePopupCount > 0)
                return;

            PopupOverlay.BeginAnimation(OpacityProperty, null);
            PopupOverlay.Opacity = 0;
            PopupOverlay.Visibility = Visibility.Collapsed;
            ClearPopupBlur();
        };
        PopupOverlay.BeginAnimation(OpacityProperty, fadeOut);
    }

    private void ApplyPopupBlur()
    {
        ContentGrid.Effect = CreatePopupBlurEffect();
        AnalyticsDrawerLayer.Effect = CreatePopupBlurEffect();
        DrawerTabHost.Effect = CreatePopupBlurEffect();
    }

    private void ClearPopupBlur()
    {
        ContentGrid.Effect = null;
        AnalyticsDrawerLayer.Effect = null;
        DrawerTabHost.Effect = null;
    }

    private static BlurEffect CreatePopupBlurEffect()
    {
        return new BlurEffect { Radius = 20, RenderingBias = RenderingBias.Performance };
    }

    private async Task ExecuteSpendingSourceSettingsActionAsync(SpendingSourceVM spendingSource,
        SettingsBatchAction action,
        bool requireDeleteConfirmation = false)
    {
        ArgumentNullException.ThrowIfNull(spendingSource);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var appData = scope.ServiceProvider.GetRequiredService<IAppDataService>();

            if (requireDeleteConfirmation)
            {
                var confirmationMessage = await SpendingSourceDeletionConfirmationHelper.BuildDeleteConfirmationMessageAsync(
                    appData,
                    spendingSource.Id,
                    spendingSource.Name);

                if (_dialogService.ShowWarning(confirmationMessage, "Settings", this, MessageBoxButton.YesNo) !=
                    MessageBoxResult.Yes)
                    return;
            }

            var settingsViewModel = scope.ServiceProvider.GetRequiredService<SettingsVM>();
            await settingsViewModel.LoadAsync();

            var result = await settingsViewModel.ExecuteSpendingSourceItemActionAsync(spendingSource.Id, action);
            if (!result.IsSuccess)
                _dialogService.ShowInformation(result.ErrorMessage, "Settings", this);
        }
        catch (Exception exception)
        {
            FluxoLogManager.LogError(exception, "Unable to update spending source from settings workflow.");
            _dialogService.ShowError(FluxoLogManager.CreateFailureMessage("update spending source"), "Settings", this);
        }
    }

    private void OpenHeaderMenu(bool pinned)
    {
        _isHeaderMenuPinned = pinned || _isHeaderMenuPinned;
        _headerMenuCloseTimer.Stop();
        HeaderMenuPopup.IsOpen = true;
    }

    private void CloseHeaderMenu()
    {
        _headerMenuCloseTimer.Stop();
        _isHeaderMenuPinned = false;
        _isPointerOverHeaderMenuButton = false;
        _isPointerOverHeaderMenuPopup = false;
        HeaderMenuPopup.IsOpen = false;
    }

    private void ScheduleHeaderMenuClose()
    {
        if (_isHeaderMenuPinned)
            return;

        _headerMenuCloseTimer.Stop();
        _headerMenuCloseTimer.Start();
    }

    private void OnHeaderMenuCloseTimerTick(object? sender, EventArgs e)
    {
        _headerMenuCloseTimer.Stop();

        if (_isHeaderMenuPinned || _isPointerOverHeaderMenuButton || _isPointerOverHeaderMenuPopup)
            return;

        HeaderMenuPopup.IsOpen = false;
    }

    private void OnWindowPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source)
            return;

        if (_isHeaderSearchExpanded && !IsDescendantOf(source, HeaderSearchRegion))
            CollapseHeaderSearch();

        if (!_isHeaderMenuPinned)
            return;

        if (FindAncestor<BalloonButton>(source) == HeaderMenuButton)
            return;

        CloseHeaderMenu();
    }

    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        CollapseHeaderSearch();
        CloseHeaderMenu();
    }

    private async Task UndoLogMemoryAsync()
    {
        if (!_logMemoryManager.CanUndo)
            return;

        try
        {
            await _logMemoryManager.UndoAsync();
        }
        catch (Exception exception)
        {
            FluxoLogManager.LogError(exception, "Unable to undo the last action.");
            _dialogService.ShowError(FluxoLogManager.CreateFailureMessage("undo last action"), "Undo", this);
        }
    }

    private async Task RedoLogMemoryAsync()
    {
        if (!_logMemoryManager.CanRedo)
            return;

        try
        {
            await _logMemoryManager.RedoAsync();
        }
        catch (Exception exception)
        {
            FluxoLogManager.LogError(exception, "Unable to redo the last action.");
            _dialogService.ShowError(FluxoLogManager.CreateFailureMessage("redo last action"), "Redo", this);
        }
    }

    private static bool IsTextInputElementFocused()
    {
        return Keyboard.FocusedElement is TextBoxBase or PasswordBox or ComboBox;
    }

    private bool IsDashboardSpendingAmountGateLocked()
    {
        return _mainVM.IsDashboardSpendingAmountGateLocked;
    }

    private bool IsSufficientFundsActionGateLocked()
    {
        return _mainVM.IsSufficientFundsActionGateLocked;
    }

    private void OnHistoryManagerStateChanged(object? sender, EventArgs e)
    {
        UpdateHistoryAvailability();
    }

    private void UpdateHistoryAvailability()
    {
        if (UndoMenuButton is not null)
            UndoMenuButton.IsEnabled = _logMemoryManager.CanUndo;

        if (RedoMenuButton is not null)
            RedoMenuButton.IsEnabled = _logMemoryManager.CanRedo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }
}