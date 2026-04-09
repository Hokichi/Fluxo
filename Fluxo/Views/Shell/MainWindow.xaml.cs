using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Fluxo.ViewModels.Shell;

namespace Fluxo.Views.Shell;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const int FadeDuration = 180; // ms
    private const int StateChangeDuration = 300; // ms
    private readonly MainVM _mainVM;
    private bool _hasCompletedPendingDeletionCleanup;
    private bool _isClosing;
    private bool _isManuallyMaximized;
    private Rect _restoreBounds;

    public MainWindow(MainVM mainVM)
    {
        InitializeComponent();

        _mainVM = mainVM;
        DataContext = _mainVM;

        Loaded += async (_, _) =>
        {
            UpdateExpandRestoreButtonIcon();
            FadeIn();
            await _mainVM.Initialize();
        };

        Closing += OnWindowClosing;
        StateChanged += OnWindowStateChanged;
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

        if (_isClosing)
            return;

        _isClosing = true;

        try
        {
            await ((App)Application.Current).DeleteMarkedExpenseLogsAsync(_mainVM);
            _hasCompletedPendingDeletionCleanup = true;
        }
        finally
        {
            Close();
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

    private void OnMaximizeWindow(object sender, ExecutedRoutedEventArgs e)
    {
        AnimateToMaximized();
    }

    private void OnRestoreWindow(object sender, ExecutedRoutedEventArgs e)
    {
        AnimateToRestored();
    }

    private void OnExpandRestoreWindow(object sender, RoutedEventArgs e)
    {
        if (_isManuallyMaximized)
            AnimateToRestored();
        else
            AnimateToMaximized();
    }

    // ── Maximize / Restore animation ────────────────────────────────

    private void OnWindowStateChanged(object sender, EventArgs e)
    {
        // If the OS maximized us (e.g. Win+Up, snap), sync our state
        if (WindowState == WindowState.Maximized && !_isManuallyMaximized)
        {
            _restoreBounds = RestoreBounds;
            WindowState = WindowState.Normal;

            var workArea = GetMonitorWorkArea();
            Left = workArea.Left;
            Top = workArea.Top;
            Width = workArea.Width;
            Height = workArea.Height;
            _isManuallyMaximized = true;

            RootBorder.CornerRadius = new CornerRadius(0);
            RootBorder.BorderThickness = new Thickness(0);
        }

        UpdateExpandRestoreButtonIcon();
    }

    private void UpdateExpandRestoreButtonIcon()
    {
        if (ExpandRestoreButton is null)
            return;

        var iconKey = _isManuallyMaximized ? "CompressAlt" : "ExpandAlt";
        ExpandRestoreButton.ButtonIcon = (Geometry)FindResource(iconKey);
    }

    private void AnimateToMaximized()
    {
        if (_isManuallyMaximized)
            return;

        // Clear held animations so Left/Top/Width/Height return real values
        ClearBoundsAnimations();

        _restoreBounds = new Rect(Left, Top, Width, Height);
        _isManuallyMaximized = true;

        var workArea = GetMonitorWorkArea();
        AnimateBounds(workArea, maximizing: true);
    }

    private void AnimateToRestored()
    {
        if (!_isManuallyMaximized)
            return;

        _isManuallyMaximized = false;
        AnimateBounds(_restoreBounds, maximizing: false);
    }

    private void AnimateBounds(Rect target, bool maximizing)
    {
        UpdateExpandRestoreButtonIcon();

        RootBorder.CornerRadius = maximizing ? new CornerRadius(0) : new CornerRadius(16);
        RootBorder.BorderThickness = maximizing ? new Thickness(0) : new Thickness(1);

        var ease = new CubicEase
        {
            EasingMode = maximizing ? EasingMode.EaseOut : EasingMode.EaseInOut
        };
        var duration = TimeSpan.FromMilliseconds(StateChangeDuration);

        // Clear previous animations so From values are read correctly
        ClearBoundsAnimations();

        var leftAnim = new DoubleAnimation(target.Left, duration) { EasingFunction = ease };
        var topAnim = new DoubleAnimation(target.Top, duration) { EasingFunction = ease };
        var widthAnim = new DoubleAnimation(target.Width, duration) { EasingFunction = ease };
        var heightAnim = new DoubleAnimation(target.Height, duration) { EasingFunction = ease };

        // On completion, commit final values as local values so the window
        // is freely movable and future animations read correct positions
        heightAnim.Completed += (_, _) =>
        {
            ClearBoundsAnimations();
            Left = target.Left;
            Top = target.Top;
            Width = target.Width;
            Height = target.Height;
        };

        BeginAnimation(LeftProperty, leftAnim);
        BeginAnimation(TopProperty, topAnim);
        BeginAnimation(WidthProperty, widthAnim);
        BeginAnimation(HeightProperty, heightAnim);
    }

    private void ClearBoundsAnimations()
    {
        BeginAnimation(LeftProperty, null);
        BeginAnimation(TopProperty, null);
        BeginAnimation(WidthProperty, null);
        BeginAnimation(HeightProperty, null);
    }

    // ── Monitor work area (multi-monitor aware) ─────────────────────

    private Rect GetMonitorWorkArea()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var monitor = MonitorFromWindow(hwnd, 2 /* MONITOR_DEFAULTTONEAREST */);
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        GetMonitorInfo(monitor, ref info);

        var source = PresentationSource.FromVisual(this);
        var toDevice = source?.CompositionTarget?.TransformFromDevice
                       ?? Matrix.Identity;

        return new Rect(
            info.rcWork.Left * toDevice.M11,
            info.rcWork.Top * toDevice.M22,
            (info.rcWork.Right - info.rcWork.Left) * toDevice.M11,
            (info.rcWork.Bottom - info.rcWork.Top) * toDevice.M22);
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
}