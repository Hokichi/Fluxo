using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Fluxo.Core.Enums;
using Fluxo.Services.Dialogs;
using Fluxo.ViewModels.Shell.Main;

namespace Fluxo.Views.Shell.Main.Pages;

public partial class Ledger : UserControl
{
    private static readonly Thickness LedgerTransactionsListDefaultMargin = new(0, 0, 0, 0);
    private static readonly Thickness LedgerTransactionsListScrollableMargin = new(0, 0, -32, 0);
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

    public void PublishViewMode()
    {
        if (DataContext is not LedgerVM viewModel)
            return;

        viewModel.ViewModeToggle.SetSelectedMainContentViewCommand.Execute(
            viewModel.ViewModeToggle.SelectedMainContentViewMode);
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

    private void OnFilterOptionPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;

        if (sender is not FrameworkElement { DataContext: object option })
            return;

        switch (option)
        {
            case LedgerFilterOption<LedgerTransactionKind> type:
                type.IsChecked = !type.IsChecked;
                break;
            case LedgerFilterOption<int> integerOption:
                integerOption.IsChecked = !integerOption.IsChecked;
                break;
            case LedgerFilterOption<ExpenseCategory> category:
                category.IsChecked = !category.IsChecked;
                break;
        }
    }

    private void OnTransactionTagPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: LedgerTransactionItemVM transaction } ||
            DataContext is not LedgerVM viewModel ||
            transaction.TagId <= 0)
            return;

        e.Handled = true;
        viewModel.ApplyTagFilter(transaction.TagId);
    }

    private void OnTransactionSpendingSourcePreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: LedgerTransactionItemVM transaction } ||
            DataContext is not LedgerVM viewModel ||
            transaction.SpendingSourceId <= 0)
            return;

        e.Handled = true;
        viewModel.ApplySpendingSourceFilter(transaction.SpendingSourceId);
    }

    private async void OnFilterDropDownClosed(object sender, EventArgs e)
    {
        if (DataContext is not LedgerVM viewModel)
            return;

        if (!viewModel.HasPendingFilterChanges)
            return;

        await ShowFilterRefreshToastAsync(viewModel.ApplyFilters);
    }

    private async void OnClearFiltersClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not LedgerVM viewModel)
            return;

        await ShowFilterRefreshToastAsync(viewModel.ClearFilters);
    }

    private Task ShowFilterRefreshToastAsync(Action refreshAction)
    {
        return _dialogService.ShowToastWhileAsync(
            "Filtering...",
            async () =>
            {
                await Dispatcher.InvokeAsync(
                    refreshAction,
                    DispatcherPriority.Render);
                await Dispatcher.InvokeAsync(
                    () => LedgerTransactionsList.Items.Refresh(),
                    DispatcherPriority.ContextIdle);
            },
            Window.GetWindow(this));
    }

    private async void OnOrderingButtonClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not LedgerVM viewModel)
            return;

        await _dialogService.ShowToastWhileAsync(
            "Ordering...",
            async () =>
            {
                await Dispatcher.InvokeAsync(
                    () => viewModel.ToggleAmountSortDirectionCommand.Execute(null),
                    DispatcherPriority.Render);
                await Dispatcher.InvokeAsync(
                    () => LedgerTransactionsList.Items.Refresh(),
                    DispatcherPriority.ContextIdle);
            },
            Window.GetWindow(this));
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

    private void OnLedgerTransactionsListScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        LedgerTransactionsList.Margin = e.ExtentHeight > e.ViewportHeight
            ? LedgerTransactionsListScrollableMargin
            : LedgerTransactionsListDefaultMargin;
    }
}
