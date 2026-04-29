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
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Popups;
using Fluxo.ViewModels.Popups.Settings;
using Fluxo.ViewModels.Shell;
using Fluxo.Views.CustomControls;
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
    private const int FadeDuration = 180; // ms
    private const int StateChangeDuration = 100; // ms
    private const int AnalyticsDrawerTransitionDuration = 220; // ms
    private const int AnalyticsDrawerTabFadeDuration = 180; // ms
    private readonly DispatcherTimer _headerMenuCloseTimer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private readonly DispatcherTimer _popupOverlayDeferredHideTimer = new() { Interval = TimeSpan.FromMilliseconds(FadeDuration) };
    private readonly IDataOperationRunner _dataOperationRunner;
    private readonly LogMemoryManager _logMemoryManager;
    private readonly MainVM _mainVM;
    private readonly IDialogService _dialogService;
    private readonly IServiceProvider _serviceProvider;
    private readonly PopupOverlayHandoffState _popupOverlayHandoffState = new();
    private readonly ObservableCollection<ExpenseLogVM> _headerSearchResults = [];
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

    private EventHandler? _renderHandler;

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
            : new UIElement[] { ContentGrid, AnalyticsDrawerLayer, AnalyticsDrawerTabHost };
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
                FadeContentIn(() => _isStateChangeTransitionActive = false);
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
            _dialogService.ShowError(
                $"Unable to initialize dashboard panels.\n\n{exception.Message}",
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

    // ── Keyboard shortcuts ────────────────────────────────────────────

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_isHeaderSearchExpanded && e.Key == Key.Escape)
        {
            CollapseHeaderSearch();
            e.Handled = true;
            return;
        }

        if (_isAnalyticsDrawerOpen && e.Key == Key.Escape)
        {
            CloseAnalyticsDrawer();
            e.Handled = true;
            return;
        }

        if (MainWindowShortcutMatcher.IsOpenQuickAddShortcut(e.Key, Keyboard.Modifiers))
        {
            OpenAddNewTransactionPopup();
            e.Handled = true;
            return;
        }

        if (MainWindowShortcutMatcher.IsOpenSearchShortcut(e.Key, Keyboard.Modifiers))
        {
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
            OpenPlanningPopup();
            e.Handled = true;
            return;
        }

        if (MainWindowShortcutMatcher.IsOpenAnalyticsShortcut(e.Key, Keyboard.Modifiers))
        {
            _ = OpenAnalyticsPopupAsync();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z && !IsTextInputElementFocused() &&
            _logMemoryManager.CanUndo)
        {
            _ = UndoLogMemoryAsync();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Y && !IsTextInputElementFocused() &&
            _logMemoryManager.CanRedo)
        {
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
        ExpandHeaderSearch();
    }

    private void OnHeaderQuickAddButtonClick(object sender, RoutedEventArgs e)
    {
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
        if (sender is not FrameworkElement { DataContext: ExpenseLogVM log })
            return;

        CollapseHeaderSearch();
        OpenExpenseDetailPopup(log);
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
        var matches = HeaderQuickSearchEngine.Search(_mainVM.BudgetPanel.GetAllExpenseLogs(), query).ToList();

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
        await UndoLogMemoryAsync();
    }

    private async void OnRedoButtonClick(object sender, RoutedEventArgs e)
    {
        CloseHeaderMenu();
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
        OpenPlanningPopup();
    }

    private async void OnAnalyticsDrawerTabClick(object sender, RoutedEventArgs e)
    {
        CloseHeaderMenu();

        if (_isAnalyticsDrawerOpen)
        {
            CloseAnalyticsDrawer();
            return;
        }

        await OpenAnalyticsPopupAsync();
    }

    private void OnCloseAnalyticsDrawerButtonClick(object sender, RoutedEventArgs e)
    {
        CloseAnalyticsDrawer();
    }

    private void OnAddSpendingSourceButtonClick(object sender, RoutedEventArgs e)
    {
        OpenAddSpendingSourcePopup();
    }

    public void OpenQuickAddPopup(QuickAddVM.QuickAddDraft? draft = null)
    {
        if (draft is { } popupDraft)
        {
            OpenAddNewTransactionPopup(popupDraft);
            return;
        }

        _dialogService.ShowQuickAdd(this);
    }

    public void OpenAddNewTransactionPopup(QuickAddVM.QuickAddDraft? draft = null)
    {
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

    public void OpenSpendingSourcesListPopup()
    {
        _dialogService.ShowSpendingSourcesList(this);
    }

    public void OpenAddSpendingSourcePopup()
    {
        _dialogService.ShowAddSpendingSource(this);
    }

    public void OpenAddFixedExpensePopup()
    {
        _dialogService.ShowAddFixedExpense(this);
    }

    public void OpenAddSavingGoalPopup()
    {
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
        _dialogService.ShowPlanningPopup(this);
    }

    public void OpenAnalyticsPopup()
    {
        _ = OpenAnalyticsPopupAsync();
    }

    private async Task OpenAnalyticsPopupAsync()
    {
        if (_isAnalyticsDrawerOpen || _isAnalyticsDrawerTransitionActive || _isPreparingAnalyticsOpen)
            return;

        _isPreparingAnalyticsOpen = true;
        AnalyticsDrawerTabButton.IsEnabled = false;

        try
        {
            EnsureAnalyticsDrawerLoaded();
            ApplyMainWindowRangeToAnalyticsIfBounded();

            if (_analyticsDrawerView is null)
                return;

            await _dialogService.ShowToastWhileAsync(
                "Loading analytics",
                () => _analyticsDrawerView.PrepareForOpenAsync(showInternalToast: false),
                this);

            OpenAnalyticsDrawer();
        }
        catch (Exception exception)
        {
            _dialogService.ShowError($"Unable to open analytics.\n\n{exception.Message}", "Analytics", this);
        }
        finally
        {
            _isPreparingAnalyticsOpen = false;

            if (!_isAnalyticsDrawerOpen && !_isAnalyticsDrawerTransitionActive)
                AnalyticsDrawerTabButton.IsEnabled = true;
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
        if (_analyticsDrawerView is not null)
            return;

        _analyticsDrawerScope = _serviceProvider.CreateScope();
        _analyticsDrawerView = _analyticsDrawerScope.ServiceProvider.GetRequiredService<Analytics>();
        AnalyticsDrawerContentHost.Content = _analyticsDrawerView;
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
        AnalyticsDrawerTabButton.IsEnabled = false;

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
                AnalyticsDrawerTabButton.IsEnabled = false;
            }
            else
            {
                AnalyticsDrawerPanel.Visibility = Visibility.Collapsed;
                AnalyticsDrawerPanel.IsHitTestVisible = false;
                AnalyticsDrawerTransform.Y = panelHeight;
                SetAnalyticsDrawerTabVisibility(visible: true, animate: false, onCompleted: () =>
                {
                    AnalyticsDrawerTabButton.IsEnabled = true;
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
                    AnalyticsDrawerTabButton.IsEnabled = true;
                });
            }
            else
            {
                AnalyticsDrawerTabButton.IsEnabled = false;
            }
        };

        AnalyticsDrawerTransform.BeginAnimation(TranslateTransform.YProperty, animation);
    }

    private void SetAnalyticsDrawerTabVisibility(bool visible, bool animate, Action? onCompleted = null)
    {
        if (ShouldReduceMotion())
            animate = false;

        _analyticsDrawerTabVisibilityToken++;
        var visibilityToken = _analyticsDrawerTabVisibilityToken;
        _isAnalyticsDrawerTabVisibilityTransitionActive = false;

        AnalyticsDrawerTabHost.BeginAnimation(OpacityProperty, null);

        if (!animate)
        {
            AnalyticsDrawerTabHost.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            AnalyticsDrawerTabHost.IsHitTestVisible = visible;
            AnalyticsDrawerTabHost.Opacity = visible ? 1d : 0d;
            onCompleted?.Invoke();
            return;
        }

        if (visible)
        {
            AnalyticsDrawerTabHost.Visibility = Visibility.Visible;
            AnalyticsDrawerTabHost.IsHitTestVisible = true;
        }
        else
        {
            AnalyticsDrawerTabHost.IsHitTestVisible = false;
        }

        var fromOpacity = visible ? AnalyticsDrawerTabHost.Opacity : 1d;
        var toOpacity = visible ? 1d : 0d;

        if (Math.Abs(fromOpacity - toOpacity) < 0.001d)
        {
            if (!visible)
                AnalyticsDrawerTabHost.Visibility = Visibility.Collapsed;

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
                AnalyticsDrawerTabHost.Visibility = Visibility.Collapsed;

            onCompleted?.Invoke();
        };

        AnalyticsDrawerTabHost.BeginAnimation(OpacityProperty, tabAnimation);
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
        _isAnalyticsDrawerOpen = false;
        _isAnalyticsDrawerTransitionActive = false;
        SetAnalyticsDrawerTabVisibility(visible: true, animate: false);
        AnalyticsDrawerTabButton.IsEnabled = true;
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
        AnalyticsDrawerTabHost.Effect = CreatePopupBlurEffect();
    }

    private void ClearPopupBlur()
    {
        ContentGrid.Effect = null;
        AnalyticsDrawerLayer.Effect = null;
        AnalyticsDrawerTabHost.Effect = null;
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

        if (requireDeleteConfirmation &&
            _dialogService.ShowWarning($"Delete \"{spendingSource.Name}\"?", "Settings", this, MessageBoxButton.YesNo) !=
            MessageBoxResult.Yes)
            return;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var settingsViewModel = scope.ServiceProvider.GetRequiredService<SettingsVM>();
            await settingsViewModel.LoadAsync();

            var result = await settingsViewModel.ExecuteSpendingSourceItemActionAsync(spendingSource.Id, action);
            if (!result.IsSuccess)
                _dialogService.ShowInformation(result.ErrorMessage, "Settings", this);
        }
        catch (Exception exception)
        {
            _dialogService.ShowError($"Unable to update this spending source.\n\n{exception.Message}", "Settings", this);
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
            _dialogService.ShowError($"Unable to undo the last action.\n\n{exception.Message}", "Undo", this);
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
            _dialogService.ShowError($"Unable to redo the last action.\n\n{exception.Message}", "Redo", this);
        }
    }

    private static bool IsTextInputElementFocused()
    {
        return Keyboard.FocusedElement is TextBoxBase or PasswordBox or ComboBox;
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
