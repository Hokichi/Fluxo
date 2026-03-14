using System.Windows;
using System.Windows.Input;
using Fluxo.ViewModels.Shell;

namespace Fluxo.Views.Shell
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainVM _mainVM;

        public MainWindow(MainVM mainVM)
        {
            InitializeComponent();

            _mainVM = mainVM;
            DataContext = _mainVM;
            Loaded += (_, _) => _mainVM.Initialize();
        }

        private void MainWindow_OnMouseMove(object sender, MouseEventArgs e)
        {
            if (Mouse.LeftButton != MouseButtonState.Pressed)
                return;

            try
            {
                DragMove();
            }
            catch (Exception exception)
            {
            }
        }
        private void OnCloseWindow(object sender, ExecutedRoutedEventArgs e)
            => SystemCommands.CloseWindow(this);

        private void OnMinimizeWindow(object sender, ExecutedRoutedEventArgs e)
            => SystemCommands.MinimizeWindow(this);

        private void OnMaximizeWindow(object sender, ExecutedRoutedEventArgs e)
            => SystemCommands.MaximizeWindow(this);

        private void OnRestoreWindow(object sender, ExecutedRoutedEventArgs e)
            => SystemCommands.RestoreWindow(this);
    }
}