using System.Windows;
using System.Windows.Input;

namespace Fluxo.Views.Shell
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
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