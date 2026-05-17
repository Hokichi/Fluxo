using Fluxo.Installer.ViewModels;

namespace Fluxo.Installer.Views;

public partial class MainWindow : System.Windows.Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        WireCloseAction();
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        WireCloseAction();
    }

    private void WireCloseAction()
    {
        if (DataContext is InstallerViewModel installerViewModel)
        {
            installerViewModel.SetCloseAction(Close);
        }
    }
}
