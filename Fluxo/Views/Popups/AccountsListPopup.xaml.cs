using System.Windows;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Shell;
using Fluxo.Views.Shell;
using Fluxo.Views.Shell.Main;
using MainVM = Fluxo.ViewModels.Shell.Main.MainVM;

namespace Fluxo.Views.Popups;

public partial class AccountsListPopup : BasePopup
{
    public AccountsListPopup(MainVM mainViewModel)
    {
        InitializeComponent();

        SourcesList.ItemsSource = mainViewModel.BudgetPanel.Accounts
            .OrderByDescending(source => source.PinnedOnUI)
            .ThenBy(source => source.Name)
            .ToList();
    }

    private void OnSourceClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: AccountVM account })
            return;

        var ownerWindow = Owner as MainWindow;
        CloseForPopupHandoff();

        ownerWindow?.Dispatcher.BeginInvoke(new Action(() =>
            ownerWindow.OpenAccountDetailPopup(account)));
    }

    private void OnAddNewSourceClick(object sender, RoutedEventArgs e)
    {
        var ownerWindow = Owner as MainWindow;
        CloseForPopupHandoff();

        ownerWindow?.Dispatcher.BeginInvoke(new Action(() =>
            ownerWindow.OpenAddAccountPopup()));
    }
}
