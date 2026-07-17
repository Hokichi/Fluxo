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

    private void OnNewAccountClick(object sender, RoutedEventArgs e)
    {
        OpenSelectedPopup(mainWindow => mainWindow.OpenAddAccountPopup());
    }

    private void OnNewTransactionClick(object sender, RoutedEventArgs e)
    {
        OpenSelectedPopup(mainWindow => mainWindow.OpenAddNewTransactionPopup());
    }

    private void OnNewSavingGoalClick(object sender, RoutedEventArgs e)
    {
        OpenSelectedPopup(mainWindow => mainWindow.OpenAddSavingGoalPopup());
    }

    private void OnRunSetupWizardClick(object sender, RoutedEventArgs e)
    {
        OpenSelectedPopup(mainWindow => mainWindow.OpenQuickSetupWizardPopup());
    }

    private void OnCheckForUpdatesClick(object sender, RoutedEventArgs e)
    {
        OpenSelectedPopup(mainWindow => _ = mainWindow.CheckForUpdatesFromQuickAccessAsync());
    }

    private void OnViewAccountsClick(object sender, RoutedEventArgs e)
    {
        OpenSelectedPopup(mainWindow => mainWindow.OpenAccountsListPopup());
    }

    private void OnPlanningReportClick(object sender, RoutedEventArgs e)
    {
        OpenSelectedPopup(mainWindow => mainWindow.OpenPlanningReport());
    }

    private void OnBudgetForecastClick(object sender, RoutedEventArgs e)
    {
        OpenSelectedPopup(mainWindow => mainWindow.OpenBudgetForecast());
    }

    private void OnOpenSettingsClick(object sender, RoutedEventArgs e)
    {
        OpenSelectedPopup(mainWindow => mainWindow.OpenSettingsPopup());
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
