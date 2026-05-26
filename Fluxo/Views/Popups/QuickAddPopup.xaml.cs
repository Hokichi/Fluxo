using System.Windows;
using MainVM = Fluxo.ViewModels.Shell.Main.MainVM;
using Fluxo.Views.Shell.Main;

namespace Fluxo.Views.Popups;

public partial class QuickAddPopup : BasePopup
{
    public QuickAddPopup(MainVM mainViewModel)
    {
        InitializeComponent();
        DataContext = mainViewModel;
    }

    private void OnNewSpendingSourceClick(object sender, RoutedEventArgs e)
    {
        OpenSelectedPopup(mainWindow => mainWindow.OpenAddSpendingSourcePopup());
    }

    private void OnNewTransactionClick(object sender, RoutedEventArgs e)
    {
        OpenSelectedPopup(mainWindow => mainWindow.OpenAddNewTransactionPopup());
    }

    private void OnNewSavingGoalClick(object sender, RoutedEventArgs e)
    {
        OpenSelectedPopup(mainWindow => mainWindow.OpenAddSavingGoalPopup());
    }

    private void OpenSelectedPopup(Action<MainWindow> openPopupAction)
    {
        if (Owner is not MainWindow ownerWindow)
        {
            Close();
            return;
        }

        CloseForPopupHandoff();
        ownerWindow.Dispatcher.BeginInvoke(new Action(() => openPopupAction(ownerWindow)));
    }
}
