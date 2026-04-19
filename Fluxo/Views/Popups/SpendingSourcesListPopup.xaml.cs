using System.Windows;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Shell;
using Fluxo.Views.CustomControls;
using Fluxo.Views.Shell;
using Fluxo.Views.Shell.Main;

namespace Fluxo.Views.Popups;

public partial class SpendingSourcesListPopup : BasePopup
{
    public SpendingSourcesListPopup(MainVM mainViewModel)
    {
        InitializeComponent();

        SourcesList.ItemsSource = mainViewModel.BudgetPanel.SpendingSources
            .OrderByDescending(source => source.ShowOnUI)
            .ThenBy(source => source.Name)
            .ToList();
    }

    private void OnSourceClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: SpendingSourceVM spendingSource })
            return;

        var ownerWindow = Owner as MainWindow;
        Close();

        ownerWindow?.Dispatcher.BeginInvoke(new Action(() =>
            ownerWindow.OpenSpendingSourceDetailPopup(spendingSource)));
    }
}