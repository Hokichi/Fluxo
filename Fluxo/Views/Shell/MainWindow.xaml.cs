using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
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
    private const int StateChangeDuration = 250; // ms
    private readonly MainVM _mainVM;
    private bool _hasCompletedPendingDeletionCleanup;

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
        AnimateStateChange(maximizing: true);
        SystemCommands.MaximizeWindow(this);
    }

    private void OnRestoreWindow(object sender, ExecutedRoutedEventArgs e)
    {
        AnimateStateChange(maximizing: false);
        SystemCommands.RestoreWindow(this);
    }

    private void OnExpandRestoreWindow(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            AnimateStateChange(maximizing: false);
            SystemCommands.RestoreWindow(this);
            return;
        }

        AnimateStateChange(maximizing: true);
        SystemCommands.MaximizeWindow(this);
    }

    // ── Restore fade-in ─────────────────────────────────────────────

    private void OnWindowStateChanged(object sender, EventArgs e)
    {
        UpdateExpandRestoreButtonIcon();
        UpdateBorderForState();
    }

    private void UpdateExpandRestoreButtonIcon()
    {
        if (ExpandRestoreButton is null)
            return;

        var iconKey = WindowState == WindowState.Maximized ? "CompressAlt" : "ExpandAlt";
        ExpandRestoreButton.ButtonIcon = (Geometry)FindResource(iconKey);
    }

    private void AnimateStateChange(bool maximizing)
    {
        var ease = new CubicEase
        {
            EasingMode = maximizing ? EasingMode.EaseOut : EasingMode.EaseInOut
        };
        var duration = TimeSpan.FromMilliseconds(StateChangeDuration);

        // Scale: start slightly smaller when maximizing, slightly larger when restoring
        var fromScale = maximizing ? 0.95 : 1.03;
        var scaleAnim = new DoubleAnimation(fromScale, 1.0, duration) { EasingFunction = ease };

        if (RootBorder.RenderTransform is ScaleTransform transform)
        {
            transform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            transform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        }
    }

    private void UpdateBorderForState()
    {
        if (RootBorder is null)
            return;

        if (WindowState == WindowState.Maximized)
        {
            RootBorder.CornerRadius = new CornerRadius(0);
            RootBorder.BorderThickness = new Thickness(0);
        }
        else
        {
            RootBorder.CornerRadius = new CornerRadius(16);
            RootBorder.BorderThickness = new Thickness(1);
        }
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