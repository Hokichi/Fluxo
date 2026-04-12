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
using Fluxo.Resources.CustomControls;
using Fluxo.Core.Interfaces;
using Fluxo.Services.History;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Popups;
using Fluxo.ViewModels.Shell;
using Fluxo.Views.Popups;

namespace Fluxo.Views.Shell;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const int FadeDuration = 180; // ms
    private const int StateChangeDuration = 200; // ms
    private readonly IExpenseCleanupService _expenseCleanupService;
    private readonly MainVM _mainVM;
    private readonly Func<IUnitOfWork> _unitOfWorkFactory;
    private bool _hasCompletedPendingDeletionCleanup;
    private bool _isClosing;
    private bool _isMaximized;
    private Rect _currentBounds;
    private readonly DispatcherTimer _headerMenuCloseTimer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private bool _isHeaderMenuPinned;
    private bool _isPointerOverHeaderMenuButton;
    private bool _isPointerOverHeaderMenuPopup;
    private readonly LogMemoryManager _logMemoryManager;
    private Rect _restoreBounds;

    public MainWindow(MainVM mainVM, Func<IUnitOfWork> unitOfWorkFactory, IExpenseCleanupService expenseCleanupService)
    {
        InitializeComponent();

        _mainVM = mainVM;
        _unitOfWorkFactory = unitOfWorkFactory;
        _expenseCleanupService = expenseCleanupService;
        _logMemoryManager = new LogMemoryManager(_mainVM, _unitOfWorkFactory);
        DataContext = _mainVM;
        _logMemoryManager.StateChanged += OnHistoryManagerStateChanged;
        UpdateHistoryAvailability();

        Loaded += async (_, _) =>
        {
            _currentBounds = new Rect(Left, Top, Width, Height);
            UpdateExpandRestoreButtonIcon();
            FadeIn();
            await _mainVM.Initialize();
        };

        Closing += OnWindowClosing;
        Deactivated += OnWindowDeactivated;
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

    // ── SystemCommand handlers ───────────────────────────────────────

    private void OnCloseWindow(object sender, ExecutedRoutedEventArgs e)
    {
        if (_isClosing) return;
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
            await _expenseCleanupService.DeleteMarkedExpenseLogsAsync(markedIds);
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
        FadeOut(() =>
        {
            SystemCommands.MinimizeWindow(this);
            BeginAnimation(OpacityProperty, null);
            Opacity = 1;
        });
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
        ExpandRestoreButton.ButtonIcon = (Geometry)FindResource(iconKey);
    }

    private void AnimateToMaximized()
    {
        if (_isMaximized)
            return;

        _restoreBounds = _currentBounds;
        _isMaximized = true;
        UpdateExpandRestoreButtonIcon();

        var workArea = GetMonitorWorkArea();
        _currentBounds = workArea;
        AnimateBounds(_restoreBounds, workArea, maximizing: true);
    }

    private void AnimateToRestored()
    {
        if (!_isMaximized)
            return;

        var from = _currentBounds;
        _isMaximized = false;
        UpdateExpandRestoreButtonIcon();

        _currentBounds = _restoreBounds;
        AnimateBounds(from, _restoreBounds, maximizing: false);
    }

    private EventHandler? _renderHandler;

    private void AnimateBounds(Rect from, Rect to, bool maximizing)
    {
        RootBorder.CornerRadius = maximizing ? new CornerRadius(0) : new CornerRadius(16);
        RootBorder.BorderThickness = maximizing ? new Thickness(0) : new Thickness(1);

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

            Left = from.Left + (to.Left - from.Left) * eased;
            Top = from.Top + (to.Top - from.Top) * eased;
            Width = from.Width + (to.Width - from.Width) * eased;
            Height = from.Height + (to.Height - from.Height) * eased;

            if (t >= 1.0)
            {
                CompositionTarget.Rendering -= _renderHandler;
                _renderHandler = null;
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

    // ── Shared UI helpers ───────────────────────────────────────────

    private void OnTagListPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListView listView)
            return;

        // Find the ListViewItem that was clicked
        if (e.OriginalSource is not DependencyObject source)
            return;

        var listViewItem = FindAncestor<ListViewItem>(source);
        if (listViewItem is null)
            return;

        // If the clicked item is already selected, deselect it
        if (listViewItem.IsSelected)
        {
            listView.SelectedItem = null;
            e.Handled = true;
        }
    }

    private void OnMoreTagsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MoreTagsButton?.IsChecked != true)
            return;

        if (e.AddedItems.Count == 0 && e.RemovedItems.Count == 0)
            return;

        MoreTagsButton.IsChecked = false;
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
            var popup = new QuickSearchPopup(_mainVM) { Owner = this };
            popup.ShowDialog();
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

    private void OnQuickAddButtonClick(object sender, RoutedEventArgs e)
    {
        CloseHeaderMenu();
        OpenQuickAddPopup();
    }

    private void OnHeaderMenuButtonMouseEnter(object sender, MouseEventArgs e)
    {
        _isPointerOverHeaderMenuButton = true;
        _headerMenuCloseTimer.Stop();
        OpenHeaderMenu(pinned: false);
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
        OpenHeaderMenu(pinned: true);
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
        using var unitOfWork = _unitOfWorkFactory();
        var popupViewModel = new QuickAddVM(_mainVM, unitOfWork);
        if (draft is { } popupDraft)
            popupViewModel.InitializeFromDraft(popupDraft);

        var popup = new QuickAddPopup(popupViewModel) { Owner = this };
        popup.ShowDialog();
    }

    public void OpenExpenseDetailPopup(ExpenseLogVM expenseLog)
    {
        using var unitOfWork = _unitOfWorkFactory();
        var popupViewModel = new ExpenseDetailVM(_mainVM, expenseLog, unitOfWork);
        var popup = new ExpenseDetailPopup(popupViewModel) { Owner = this };
        popup.ShowDialog();
    }

    public void OpenSpendingSourcesListPopup()
    {
        var popup = new SpendingSourcesListPopup(_mainVM) { Owner = this };
        popup.ShowDialog();
    }

    public void OpenAddSpendingSourcePopup()
    {
        var popup = new AddSpendingSourcePopup(new AddSpendingSourceVM(_mainVM, _unitOfWorkFactory))
        {
            Owner = this
        };
        popup.ShowDialog();
    }

    public void OpenSettingsPopup()
    {
        var popup = new SettingsPopup(new SettingsVM(_mainVM, _unitOfWorkFactory)) { Owner = this };
        popup.ShowDialog();
    }

    public void OpenSpendingSourceDetailPopup(SpendingSourceVM spendingSource)
    {
        using var unitOfWork = _unitOfWorkFactory();
        var popupViewModel = new SpendingSourceDetailVM(_mainVM, spendingSource.Id, unitOfWork);
        var popup = new SpendingSourceDetailPopup(popupViewModel) { Owner = this };
        popup.ShowDialog();
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
            FluxoMessageBox.Show(this, $"Unable to undo the last action.\n\n{exception.Message}", "Undo",
                MessageBoxButton.OK, MessageBoxImage.Error);
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
            FluxoMessageBox.Show(this, $"Unable to redo the last action.\n\n{exception.Message}", "Redo",
                MessageBoxButton.OK, MessageBoxImage.Error);
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
}
