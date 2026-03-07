using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Fluxo
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