using System.Windows;
using System.Windows.Controls;
using Fluxo.ViewModels.Shell.Main;

namespace Fluxo.Views.Shell.Main;

public partial class Ledger : UserControl
{
    public Ledger(LedgerVM viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public Task PrepareForOpenAsync()
    {
        return DataContext is LedgerVM viewModel
            ? viewModel.LoadAsync()
            : Task.CompletedTask;
    }

    private void OnRemoveTransactionClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: LedgerTransactionItemVM transaction } ||
            DataContext is not LedgerVM viewModel)
            return;

        var result = MessageBox.Show(
            Window.GetWindow(this),
            $"Remove {transaction.Name} from the ledger?",
            "Ledger",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        _ = viewModel.RemoveTransactionCommand.ExecuteAsync(transaction);
    }
}
