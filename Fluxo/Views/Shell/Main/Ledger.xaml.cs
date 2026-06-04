using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Fluxo.Services.Dialogs;
using Fluxo.ViewModels.Shell.Main;

namespace Fluxo.Views.Shell.Main;

public partial class Ledger : UserControl
{
    private readonly IDialogService _dialogService;
    private bool _hasLoaded;
    private bool _isApplyingGroupingSelection;

    public Ledger(LedgerVM viewModel, IDialogService dialogService)
    {
        _dialogService = dialogService;
        InitializeComponent();
        DataContext = viewModel;
        Loaded += (_, _) => _hasLoaded = true;
    }

    public Task PrepareForOpenAsync()
    {
        return DataContext is LedgerVM viewModel
            ? viewModel.LoadAsync()
            : Task.CompletedTask;
    }

    public void ApplyOpenRange(DateTime from, DateTime to)
    {
        if (DataContext is LedgerVM viewModel)
            viewModel.ApplyExternalDateRange(from, to, refresh: false);
    }

    public void ApplyAllTimeRange()
    {
        if (DataContext is LedgerVM viewModel)
            viewModel.ApplyAllTimeRange(refresh: false);
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

    private async void OnGroupingModeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_hasLoaded || _isApplyingGroupingSelection || sender is not ComboBox comboBox)
            return;

        if (comboBox.SelectedItem is not LedgerGroupingMode groupingMode ||
            DataContext is not LedgerVM viewModel ||
            groupingMode == viewModel.SelectedGroupingMode)
            return;

        _isApplyingGroupingSelection = true;
        try
        {
            await _dialogService.ShowToastWhileAsync(
                "Grouping ledger",
                async () =>
                {
                    await Dispatcher.InvokeAsync(
                        () => viewModel.SelectedGroupingMode = groupingMode,
                        DispatcherPriority.Render);
                    await Dispatcher.InvokeAsync(
                        () => LedgerTransactionsList.Items.Refresh(),
                        DispatcherPriority.ContextIdle);
                },
                Window.GetWindow(this));
        }
        finally
        {
            _isApplyingGroupingSelection = false;
        }
    }
}
