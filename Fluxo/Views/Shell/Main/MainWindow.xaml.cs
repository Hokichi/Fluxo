using System.ComponentModel;
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
using Fluxo.Core.Interfaces;
using Fluxo.Resources.CustomControls;
using Fluxo.Services.Dialogs;
using Fluxo.Services.History;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Popups;
using Fluxo.ViewModels.Shell;

namespace Fluxo.Views.Shell.Main;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window, IPopupHost
{
    private const int FadeDuration = 100; // ms
    private const int StateChangeDuration = 100; // ms
    private readonly DispatcherTimer _headerMenuCloseTimer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private readonly BudgetAllocationPanelVM _budgetAllocationPanelVM;
    private readonly LogMemoryManager _logMemoryManager;
    private readonly MainVM _mainVM;
    private readonly NotificationPanelVM _notificationPanelVM;
    private readonly SavingGoalsPanelVM _savingGoalsPanelVM;
    private readonly MainViewModeToggleVM _viewModeToggleVM;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDialogService _dialogService;
    private Rect _currentBounds;
    private bool _hasCompletedPendingDeletionCleanup;
    private bool _hasInitializedDashboardPanels;
    private bool _isHeaderMenuPinned;
    private bool _isMaximized;
    private bool _isStateChangeTransitionActive;
    private bool _wasMinimized;
    private bool _isPointerOverHeaderMenuButton;
    private bool _isPointerOverHeaderMenuPopup;

    private EventHandler? _renderHandler;

    public MainWindow(
        MainVM mainVM,
        IUnitOfWork unitOfWork,
        IDialogService dialogService,
        DaySpinnerVM daySpinnerVM,
        MainViewModeToggleVM viewModeToggleVM,
        BudgetAllocationPanelVM budgetAllocationPanelVM,
        NotificationPanelVM notificationPanelVM,
        SavingGoalsPanelVM savingGoalsPanelVM)
    {
        InitializeComponent();

        _mainVM = mainVM;
        _unitOfWork = unitOfWork;
        _dialogService = dialogService;
        _viewModeToggleVM = viewModeToggleVM;
        _budgetAllocationPanelVM = budgetAllocationPanelVM;
        _notificationPanelVM = notificationPanelVM;
        _savingGoalsPanelVM = savingGoalsPanelVM;
        _logMemoryManager = new LogMemoryManager(_mainVM, _unitOfWork);

        DataContext = _mainVM;
        DaySpinnerControlHost.DataContext = daySpinnerVM;
        ViewModeToggleControlHost.DataContext = _viewModeToggleVM;
        BudgetAllocationPanelHost.DataContext = _budgetAllocationPanelVM;
        NotificationPanelHost.DataContext = _notificationPanelVM;
        SavingGoalsPanelHost.DataContext = _savingGoalsPanelVM;
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
        if (Mouse.LeftButton != MouseButtonState.Pressed || e.GetPosition(this).Y >= 60)
            return;

        if (e.OriginalSource is DependencyObject source && IsInteractiveElement(source))
            return;

        try
        {
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
        FadeElement(ContentGrid, 0, EasingMode.EaseIn, onCompleted);
    }

    private void FadeContentIn(Action? onCompleted = null)
    {
        FadeElement(ContentGrid, 1, EasingMode.EaseOut, onCompleted);
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
            var markedIds = _mainVM.GetExpenseLogIdsMarkedForDeletion();
            _hasCompletedPendingDeletionCleanup = true;
            _logMemoryManager.StateChanged -= OnHistoryManagerStateChanged;
            _logMemoryManager.Dispose();
        }
        finally
        {
            Environment.Exit(1);
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

        var iconKey = _isMaximized ? "MainWindow.CompressAlt" : "MainWindow.ExpandAlt";
        ExpandRestoreButton.ButtonIcon = (Geometry)FindResource(iconKey);
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
        // Stop any in-progress animation
        if (_renderHandler is not null)
        {
            CompositionTarget.Rendering -= _renderHandler;
            _renderHandler = null;
        }

        // Clear any leftover WPF animations so direct property sets work
        BeginAnimation(LeftProperty, null);
        BeginAnimation(TopProperty, null);
        BeginAnimation(WidthProperty, null);
        BeginAnimation(HeightProperty, null);

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
            await _budgetAllocationPanelVM.LoadAsync();
            await _notificationPanelVM.LoadAsync();
            await _savingGoalsPanelVM.LoadAsync();
            _viewModeToggleVM.SetSelectedMainContentViewCommand.Execute(_viewModeToggleVM.SelectedMainContentViewMode);
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

    // ── Keyboard shortcuts ────────────────────────────────────────────

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control)
        {
            OpenQuickAddPopup();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            _dialogService.ShowQuickSearch(this);
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
        var popupViewModel = new QuickAddVM(_mainVM, _unitOfWork);
        if (draft is { } popupDraft)
            popupViewModel.InitializeFromDraft(popupDraft);

        _dialogService.ShowAddNewTransaction(popupViewModel, this);
    }

    public void OpenExpenseDetailPopup(ExpenseLogVM expenseLog)
    {
        var unitOfWork = _unitOfWork;
        var popupViewModel = new ExpenseDetailVM(_mainVM, expenseLog, unitOfWork);
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

    public void OpenStartupWizardPopup()
    {
        _dialogService.ShowStartupWizard(this);
    }

    public void OpenSpendingSourceDetailPopup(SpendingSourceVM spendingSource)
    {
        var unitOfWork = _unitOfWork;
        var popupViewModel = new SpendingSourceDetailVM(_mainVM, spendingSource.Id, unitOfWork);
        _dialogService.ShowSpendingSourceDetail(popupViewModel, this);
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

        var transferVm = new TransferFundsVM(_mainVM, spendingSource, _unitOfWork);
        _dialogService.ShowTransferFunds(transferVm, this);
    }

    // ── Popup overlay & blur ────────────────────────────────────────

    public void ShowPopupOverlay()
    {
        ContentGrid.Effect = new BlurEffect { Radius = 20, RenderingBias = RenderingBias.Performance };

        PopupOverlay.Visibility = Visibility.Visible;
        var fadeIn = new DoubleAnimation(0, 0.5, TimeSpan.FromMilliseconds(FadeDuration))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        PopupOverlay.BeginAnimation(OpacityProperty, fadeIn);
    }

    public void HidePopupOverlay()
    {
        ContentGrid.Effect = null;

        var fadeOut = new DoubleAnimation(0.5, 0, TimeSpan.FromMilliseconds(FadeDuration))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        fadeOut.Completed += (_, _) =>
        {
            PopupOverlay.BeginAnimation(OpacityProperty, null);
            PopupOverlay.Opacity = 0;
            PopupOverlay.Visibility = Visibility.Collapsed;
        };
        PopupOverlay.BeginAnimation(OpacityProperty, fadeOut);
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
            var settingsViewModel = new SettingsVM(_mainVM, _unitOfWork);
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
        if (!_isHeaderMenuPinned || e.OriginalSource is not DependencyObject source)
            return;

        if (FindAncestor<BalloonButton>(source) == HeaderMenuButton)
            return;

        CloseHeaderMenu();
    }

    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
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