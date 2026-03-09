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
    }
}