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
using Fluxo.ViewModels.Shell;
using Fluxo.Views.Popups;

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
    private bool _isMaximized;
    private Rect _currentBounds;
    private Rect _restoreBounds;

    public MainWindow(MainVM mainVM)
    {
        InitializeComponent();

        _mainVM = mainVM;
        DataContext = _mainVM;

        Loaded += async (_, _) =>
        {
            _currentBounds = new Rect(Left, Top, Width, Height);
            UpdateExpandRestoreButtonIcon();
            FadeIn();
            await _mainVM.Initialize();
        };

        Closing += OnWindowClosing;
        PreviewKeyDown += OnPreviewKeyDown;
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
            await ((App)Application.Current).DeleteMarkedExpenseLogsAsync(_mainVM);
            _hasCompletedPendingDeletionCleanup = true;
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
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            var popup = new QuickSearchPopup(_mainVM) { Owner = this };
            popup.ShowDialog();
            e.Handled = true;
        }
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
}