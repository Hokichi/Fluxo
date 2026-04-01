using Fluxo.ViewModels.Shell;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Fluxo.Views.Shell
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainVM _mainVM;
        private const int FadeDuration = 180; // ms

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

            StateChanged += OnWindowStateChanged;
        }

        private void MainWindow_OnMouseMove(object sender, MouseEventArgs e)
        {
            if (Mouse.LeftButton != MouseButtonState.Pressed)
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

        private void OnMinimizeWindow(object sender, ExecutedRoutedEventArgs e)
        {
            FadeOut(() =>
            {
                SystemCommands.MinimizeWindow(this);
                // Reset opacity immediately so it's ready when restored
                BeginAnimation(OpacityProperty, null);
                Opacity = 1;
            });
        }

        private void OnMaximizeWindow(object sender, ExecutedRoutedEventArgs e)
            => SystemCommands.MaximizeWindow(this); // no fade needed, snappy feels better

        private void OnRestoreWindow(object sender, ExecutedRoutedEventArgs e)
            => SystemCommands.RestoreWindow(this); // StateChanged handles the fade-in

        private void OnExpandRestoreWindow(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                SystemCommands.RestoreWindow(this);
                return;
            }

            SystemCommands.MaximizeWindow(this);
        }

        // ── Restore fade-in ─────────────────────────────────────────────

        private void OnWindowStateChanged(object sender, EventArgs e)
        {
            UpdateExpandRestoreButtonIcon();
        }

        private void UpdateExpandRestoreButtonIcon()
        {
            if (ExpandRestoreButton is null)
                return;

            var iconKey = WindowState == WindowState.Maximized ? "CompressAlt" : "ExpandAlt";
            ExpandRestoreButton.ButtonIcon = (Geometry)FindResource(iconKey);
        }

        private static bool IsInteractiveElement(DependencyObject source)
        {
            for (DependencyObject? current = source; current is not null; current = VisualTreeHelper.GetParent(current))
            {
                if (current is ScrollBar or Thumb or ButtonBase or ListViewItem or TextBoxBase or Selector)
                    return true;
            }

            return false;
        }
    }
}
