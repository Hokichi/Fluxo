using System.Windows;
using Fluxo.Views.CustomControls;
using Fluxo.Views.Shell.Main;

namespace Fluxo.Views.Popups;

public partial class QuickAddPopup : BasePopup
{
    public QuickAddPopup()
    {
        InitializeComponent();
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

    private void OnNewFixedExpenseClick(object sender, RoutedEventArgs e)
    {
        OpenSelectedPopup(mainWindow => mainWindow.OpenAddFixedExpensePopup());
    }

    private void OpenSelectedPopup(Action<MainWindow> openPopupAction)
    {
        if (Owner is not MainWindow ownerWindow)
        {
            Close();
            return;
        }

        Close();
        ownerWindow.Dispatcher.BeginInvoke(new Action(() => openPopupAction(ownerWindow)));
    }
}
