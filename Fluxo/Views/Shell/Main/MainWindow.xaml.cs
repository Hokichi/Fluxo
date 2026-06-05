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
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Resources.Resources.Messages;
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
using Analytics = Fluxo.Views.Shell.Main.Pages.Analytics;
using Calendar = Fluxo.Views.Shell.Main.Pages.Calendar;
using Dashboard = Fluxo.Views.Shell.Main.Pages.Dashboard;
using DateRangeResolver = Fluxo.ViewModels.Shell.Main.DateRangeResolver;
using Ledger = Fluxo.Views.Shell.Main.Pages.Ledger;
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

    private enum MainPage
    {
        Dashboard,
        Analytics,
        Calendar,
        Ledger
    }

    private const int FadeDuration = 180; // ms
    private const int StateChangeDuration = 100; // ms
    private const int MainPageTransitionDuration = 300; // ms

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
    private bool _isMainPageTransitionActive;
    private bool _isPreparingMainPage;
    private bool _isHeaderSearchExpanded;
    private EventHandler? _popupOverlayDeferredHideTickHandler;
    private MainPage _activeMainPage = MainPage.Dashboard;
    private IServiceScope? _dashboardPageScope;
    private Dashboard? _dashboardPageView;
    private IServiceScope? _analyticsPageScope;
    private Analytics? _analyticsPageView;
    private IServiceScope? _calendarPageScope;
    private Calendar? _calendarPageView;
    private IServiceScope? _ledgerPageScope;
    private Ledger? _ledgerPageView;

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

            EnsureDashboardPageLoaded();
            MainPageHost.Content = _dashboardPageView;
            UpdateMainNavigationCheckedState(_activeMainPage);
            UpdateHeaderDateSelectorEnabledState(_activeMainPage);
            _currentBounds = new Rect(Left, Top, Width, Height);
            UpdateExpandRestoreButtonIcon();
            FadeIn();
        };

        Closing += OnWindowClosing;
        Deactivated += OnWindowDeactivated;
        StateChanged += OnWindowStateChanged;
        PreviewKeyDown += OnPreviewKeyDown;
        PreviewMouseLeftButtonDown += OnWindowPreviewMouseLeftButtonDown;
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

    // â”€â”€ Fade helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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
        return [ContentGrid, MainPageHost, FloatingSideNavigationRail];
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

    // â”€â”€ SystemCommand handlers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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
            DisposeMainPages();
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

    // â”€â”€ Monitor work area â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

    // â”€â”€ Shared UI helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

    // â”€â”€ Keyboard shortcuts â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_isHeaderSearchExpanded && e.Key == Key.Escape)
        {
            CollapseHeaderSearch();
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
        if (_activeMainPage == MainPage.Ledger)
            WeakReferenceMessenger.Default.Send(new LedgerSearchTextChangedMessage(HeaderSearchBox.Text));

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

        if (_activeMainPage == MainPage.Ledger && !ShouldCollapseHeaderSearchOnExternalClick())
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
        WeakReferenceMessenger.Default.Send(new LedgerSearchTextChangedMessage(string.Empty));
        _headerSearchResults.Clear();
    }

    private void UpdateHeaderSearchResults()
    {
        if (!_isHeaderSearchExpanded)
            return;

        if (_activeMainPage == MainPage.Ledger)
        {
            HeaderSearchResultsPopup.IsOpen = false;
            HeaderSearchNoResultsText.Visibility = Visibility.Collapsed;
            _headerSearchResults.Clear();
            return;
        }

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

    private async void OnHomeNavigationClick(object sender, RoutedEventArgs e)
    {
        await NavigateToMainPageAsync(MainPage.Dashboard);
    }

    private async void OnAnalyticsNavigationClick(object sender, RoutedEventArgs e)
    {
        await NavigateToMainPageAsync(MainPage.Analytics);
    }

    private async void OnCalendarNavigationClick(object sender, RoutedEventArgs e)
    {
        await NavigateToMainPageAsync(MainPage.Calendar);
    }

    private async void OnLedgerNavigationClick(object sender, RoutedEventArgs e)
    {
        await NavigateToMainPageAsync(MainPage.Ledger);
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
        await NavigateToMainPageAsync(MainPage.Analytics);
    }

    private async Task NavigateToMainPageAsync(MainPage page)
    {
        if (page != MainPage.Dashboard && IsSufficientFundsActionGateLocked())
        {
            UpdateMainNavigationCheckedState(_activeMainPage);
            return;
        }

        if (_activeMainPage == page || _isMainPageTransitionActive || _isPreparingMainPage)
        {
            UpdateMainNavigationCheckedState(_activeMainPage);
            return;
        }

        UpdateMainNavigationCheckedState(_activeMainPage);
        _isPreparingMainPage = true;
        SetMainNavigationEnabled(false);
        CloseHeaderMenu();

        try
        {
            UIElement? nextPage = null;
            await _dialogService.ShowToastWhileAsync(
                GetMainPageLoadingMessage(page),
                async () =>
                {
                    nextPage = ResolveMainPageView(page);
                    await TransitionToMainPageAsync(nextPage);
                    await PrepareMainPageContentAsync(page);
                    _activeMainPage = page;
                    UpdateMainNavigationCheckedState(_activeMainPage);
                    UpdateHeaderDateSelectorEnabledState(_activeMainPage);
                },
                this);
        }
        catch (Exception exception)
        {
            var label = GetMainPageLabel(page);
            FluxoLogManager.LogError(exception, $"Unable to navigate to {label}.");
            _dialogService.ShowError(
                FluxoLogManager.CreateFailureMessage($"open {label}"),
                label,
                this);
            UpdateMainNavigationCheckedState(_activeMainPage);
        }
        finally
        {
            _isPreparingMainPage = false;
            SetMainNavigationEnabled(true);
        }
    }

    private UIElement ResolveMainPageView(MainPage page)
    {
        switch (page)
        {
            case MainPage.Dashboard:
                EnsureDashboardPageLoaded();
                return _dashboardPageView!;
            case MainPage.Analytics:
                EnsureAnalyticsPageLoaded();
                return _analyticsPageView!;
            case MainPage.Calendar:
                EnsureCalendarPageLoaded();
                return _calendarPageView!;
            case MainPage.Ledger:
                EnsureLedgerPageLoaded();
                return _ledgerPageView!;
            default:
                EnsureDashboardPageLoaded();
                return _dashboardPageView!;
        }
    }

    private async Task PrepareMainPageContentAsync(MainPage page)
    {
        switch (page)
        {
            case MainPage.Dashboard:
                PublishDashboardViewMode();
                if (_mainVM.Dashboard.IsInitialized)
                    await _mainVM.Dashboard.ReloadCurrentDataAsync();
                else
                    await _mainVM.Dashboard.Initialize();
                return;
            case MainPage.Analytics:
                ApplyMainWindowRangeToAnalyticsIfBounded();
                await _analyticsPageView!.PrepareForOpenAsync(showInternalToast: false);
                return;
            case MainPage.Calendar:
                await _calendarPageView!.PrepareForOpenAsync();
                return;
            case MainPage.Ledger:
                _ledgerPageView!.PublishViewMode();
                ApplyMainWindowRangeToLedger();
                await _ledgerPageView!.PrepareForOpenAsync();
                return;
            default:
                return;
        }
    }

    private void EnsureDashboardPageLoaded()
    {
        if (_dashboardPageView is not null)
            return;

        _dashboardPageScope = _serviceProvider.CreateScope();
        _dashboardPageView = _dashboardPageScope.ServiceProvider.GetRequiredService<Dashboard>();
    }

    private void EnsureAnalyticsPageLoaded()
    {
        if (_analyticsPageView is not null)
            return;

        _analyticsPageScope = _serviceProvider.CreateScope();
        _analyticsPageView = _analyticsPageScope.ServiceProvider.GetRequiredService<Analytics>();
    }

    private void EnsureCalendarPageLoaded()
    {
        if (_calendarPageView is not null)
            return;

        _calendarPageScope = _serviceProvider.CreateScope();
        _calendarPageView = _calendarPageScope.ServiceProvider.GetRequiredService<Calendar>();
    }

    private void EnsureLedgerPageLoaded()
    {
        if (_ledgerPageView is not null)
            return;

        _ledgerPageScope = _serviceProvider.CreateScope();
        _ledgerPageView = _ledgerPageScope.ServiceProvider.GetRequiredService<Ledger>();
    }

    private async Task TransitionToMainPageAsync(UIElement nextPage)
    {
        _isMainPageTransitionActive = true;
        try
        {
            await FadeElementAsync(MainPageHost, 0, EasingMode.EaseIn, MainPageTransitionDuration);

            MainPageHost.BeginAnimation(OpacityProperty, null);
            MainPageHost.Content = nextPage;
            MainPageHost.Visibility = Visibility.Visible;
            MainPageHost.Opacity = 0;

            await FadeElementAsync(MainPageHost, 1, EasingMode.EaseOut, MainPageTransitionDuration);
        }
        finally
        {
            _isMainPageTransitionActive = false;
        }
    }

    private static Task FadeElementAsync(UIElement element, double toOpacity, EasingMode easingMode, int durationMilliseconds)
    {
        element.BeginAnimation(OpacityProperty, null);

        var completion = new TaskCompletionSource();
        var animation = new DoubleAnimation(element.Opacity, toOpacity, TimeSpan.FromMilliseconds(durationMilliseconds))
        {
            EasingFunction = new CubicEase { EasingMode = easingMode }
        };

        animation.Completed += (_, _) => completion.SetResult();
        element.BeginAnimation(OpacityProperty, animation);
        return completion.Task;
    }

    private void SetMainNavigationEnabled(bool isEnabled)
    {
        HomeNavigationButton.IsEnabled = isEnabled;
        AnalyticsNavigationButton.IsEnabled = isEnabled;
        CalendarNavigationButton.IsEnabled = isEnabled;
        LedgerNavigationButton.IsEnabled = isEnabled;
    }

    private void UpdateMainNavigationCheckedState(MainPage page)
    {
        HomeNavigationButton.IsChecked = page == MainPage.Dashboard;
        AnalyticsNavigationButton.IsChecked = page == MainPage.Analytics;
        CalendarNavigationButton.IsChecked = page == MainPage.Calendar;
        LedgerNavigationButton.IsChecked = page == MainPage.Ledger;
    }

    private void UpdateHeaderDateSelectorEnabledState(MainPage page)
    {
        DaySpinnerControlHost.IsEnabled = page is not MainPage.Analytics and not MainPage.Calendar;
    }

    private static string GetMainPageLabel(MainPage page)
    {
        return page switch
        {
            MainPage.Dashboard => "Dashboard",
            MainPage.Analytics => "Analytics",
            MainPage.Calendar => "Calendar",
            MainPage.Ledger => "Ledger",
            _ => "Dashboard"
        };
    }

    private static string GetMainPageLoadingMessage(MainPage page)
    {
        return page switch
        {
            MainPage.Dashboard => "Loading Dashboard",
            MainPage.Analytics => "Loading Analytics",
            MainPage.Calendar => "Loading Calendar",
            MainPage.Ledger => "Loading Ledger",
            _ => "Loading Dashboard"
        };
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

    private void ApplyMainWindowRangeToAnalyticsIfBounded()
    {
        if (_analyticsPageView is null)
            return;

        var selectedMode = _mainVM.Dashboard.ViewModeToggle.SelectedMainContentViewMode;
        if (selectedMode == MainContentViewMode.AllTime)
            return;

        var selectedDate = _mainVM.DaySpinner.SelectedDay.Date;
        if (selectedDate == default)
            selectedDate = DateTime.Today;

        var range = DateRangeResolver.Resolve(selectedDate, selectedMode);
        _analyticsPageView.ApplyOpenRange(range.From, range.To);
    }

    private void ApplyMainWindowRangeToLedger()
    {
        if (_ledgerPageView is null || _mainVM.Ledger is null)
            return;

        var selectedMode = _mainVM.Ledger.ViewModeToggle.SelectedMainContentViewMode;
        if (selectedMode == MainContentViewMode.AllTime)
        {
            _ledgerPageView.ApplyAllTimeRange();
            return;
        }

        var selectedDate = _mainVM.DaySpinner.SelectedDay.Date;
        if (selectedDate == default)
            selectedDate = DateTime.Today;

        var range = DateRangeResolver.Resolve(selectedDate, selectedMode);
        _ledgerPageView.ApplyOpenRange(range.From, range.To);
    }

    private void PublishDashboardViewMode()
    {
        var viewModeToggle = _mainVM.Dashboard.ViewModeToggle;
        viewModeToggle.SetSelectedMainContentViewCommand.Execute(viewModeToggle.SelectedMainContentViewMode);
    }

    private static bool ShouldReduceMotion()
    {
        return !SystemParameters.ClientAreaAnimation;
    }

    private void DisposeMainPages()
    {
        MainPageHost.Content = null;

        _dashboardPageView = null;
        _dashboardPageScope?.Dispose();
        _dashboardPageScope = null;

        _analyticsPageView = null;
        _analyticsPageScope?.Dispose();
        _analyticsPageScope = null;

        _calendarPageView = null;
        _calendarPageScope?.Dispose();
        _calendarPageScope = null;

        _ledgerPageView = null;
        _ledgerPageScope?.Dispose();
        _ledgerPageScope = null;

        _activeMainPage = MainPage.Dashboard;
        _isMainPageTransitionActive = false;
        _isPreparingMainPage = false;
    }

    // â”€â”€ Popup overlay & blur â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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
        MainPageHost.Effect = CreatePopupBlurEffect();
        FloatingSideNavigationRail.Effect = CreatePopupBlurEffect();
    }

    private void ClearPopupBlur()
    {
        ContentGrid.Effect = null;
        MainPageHost.Effect = null;
        FloatingSideNavigationRail.Effect = null;
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

        if (_isHeaderSearchExpanded &&
            !IsDescendantOf(source, HeaderSearchRegion) &&
            ShouldCollapseHeaderSearchOnExternalClick())
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

    private bool ShouldCollapseHeaderSearchOnExternalClick()
    {
        return _activeMainPage != MainPage.Ledger || string.IsNullOrWhiteSpace(HeaderSearchBox.Text);
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

