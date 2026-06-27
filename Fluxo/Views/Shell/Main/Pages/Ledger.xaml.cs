using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Fluxo.Core.Enums;
using Fluxo.Resources.Infrastructure;
using Fluxo.Services.Dialogs;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Shell.Main;
using Fluxo.Views.Shell.Main;
using Microsoft.Win32;

namespace Fluxo.Views.Shell.Main.Pages;

public partial class Ledger : UserControl
{
    private readonly IDialogService _dialogService;
    private ComboBox? _filterDropDownKeepOpenComboBox;
    private bool _hasLoaded;
    private bool _isApplyingGroupingSelection;
    private bool _suppressNextFilterDropDownClose;

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

    private void OnLedgerRowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is UIElement row)
        {
            row.RemoveHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(OnLedgerRowPreviewMouseLeftButtonDown));
            row.AddHandler(
                UIElement.PreviewMouseLeftButtonDownEvent,
                new MouseButtonEventHandler(OnLedgerRowPreviewMouseLeftButtonDown),
                handledEventsToo: true);
        }
    }

    private void OnRemoveTransactionClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: LedgerTransactionItemVM transaction } ||
            DataContext is not LedgerVM viewModel)
            return;

        var result = FluxoMessageBox.Show(
            Window.GetWindow(this),
            $"Remove {transaction.Name} from the ledger?",
            "Ledger",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        _ = viewModel.RemoveTransactionCommand.ExecuteAsync(transaction);
    }

    private void OnDeleteOrDiscardTransactionClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: LedgerTransactionItemVM transaction } ||
            DataContext is not LedgerVM viewModel)
        {
            return;
        }

        if (transaction.IsEditing)
        {
            _ = viewModel.DiscardTransactionEditCommand.ExecuteAsync(transaction);
            if (sender is ToggleButton toggleButton)
                toggleButton.IsChecked = transaction.IsEditing;
            return;
        }

        OnRemoveTransactionClick(sender, e);
        if (sender is ToggleButton deleteToggleButton)
            deleteToggleButton.IsChecked = transaction.IsEditing;
    }

    private void OnLedgerRowPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: LedgerTransactionItemVM transaction } ||
            DataContext is not LedgerVM viewModel ||
            !transaction.HasChildTransactions ||
            IsInteractiveLedgerRowElement(e.OriginalSource as DependencyObject, sender as DependencyObject))
        {
            return;
        }

        viewModel.ToggleChildTransactionsCommand.Execute(transaction);
        e.Handled = true;
    }

    private static bool IsInteractiveLedgerRowElement(DependencyObject? source, DependencyObject? rowRoot)
    {
        while (source is not null)
        {
            if (ReferenceEquals(source, rowRoot))
                return false;

            if (source is TextBox { IsReadOnly: true })
            {
                source = DependencyObjectTree.GetParent(source);
                continue;
            }

            if (source is TextBox or ButtonBase or CheckBox or ComboBox or ListBox or Popup)
                return true;

            source = DependencyObjectTree.GetParent(source);
        }

        return false;
    }

    private void OnFilterOptionPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;

        if (sender is not FrameworkElement { DataContext: object option })
            return;

        var comboBox = sender is ComboBoxItem comboBoxItem
            ? ItemsControl.ItemsControlFromItemContainer(comboBoxItem) as ComboBox
            : null;

        KeepFilterDropDownOpenAfterOptionClick(comboBox);

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

    private void OnFilterOptionPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    private void KeepFilterDropDownOpenAfterOptionClick(ComboBox? comboBox)
    {
        if (comboBox is null)
            return;

        _suppressNextFilterDropDownClose = true;
        _filterDropDownKeepOpenComboBox = comboBox;

        _ = Dispatcher.BeginInvoke(() =>
        {
            comboBox.IsDropDownOpen = true;

            if (ReferenceEquals(_filterDropDownKeepOpenComboBox, comboBox) && comboBox.IsDropDownOpen)
            {
                _filterDropDownKeepOpenComboBox = null;
                _suppressNextFilterDropDownClose = false;
            }
        }, DispatcherPriority.Input);
    }

    private void OnTransactionTagPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: LedgerTransactionItemVM transaction } ||
            transaction.TagId <= 0)
            return;

        e.Handled = true;
        if (transaction.IsEditing)
        {
            OpenTransactionTagPopup(transaction);
            return;
        }

        if (DataContext is not LedgerVM viewModel)
            return;

        viewModel.ApplyTagFilter(transaction.TagId);
    }

    private void OnEditTagSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: LedgerTransactionItemVM transaction } ||
            DataContext is not LedgerVM viewModel ||
            e.AddedItems.Count == 0 ||
            e.AddedItems[0] is not TagVM tag)
        {
            return;
        }

        viewModel.ApplyTransactionTag(transaction, tag);
        if (sender is ListBox listBox)
            listBox.SelectedItem = null;
    }

    private void OnTransactionAccountPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: LedgerTransactionItemVM transaction } ||
            DataContext is not LedgerVM viewModel ||
            transaction.AccountId <= 0)
            return;

        e.Handled = true;
        if (transaction.IsEditing)
        {
            OpenTransactionAccountPopup(transaction);
            return;
        }

        viewModel.ApplyAccountFilter(transaction.AccountId);
    }

    private void OnEditAccountSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: LedgerTransactionItemVM transaction } ||
            DataContext is not LedgerVM viewModel ||
            e.AddedItems.Count == 0 ||
            e.AddedItems[0] is not AccountVM account)
        {
            return;
        }

        viewModel.ApplyTransactionAccount(transaction, account);
        transaction.IsAccountPopupOpen = false;
        if (sender is ListBox listBox)
            listBox.SelectedItem = null;
    }

    private void OnDuplicateTransactionClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: LedgerTransactionItemVM transaction } ||
            DataContext is not LedgerVM viewModel ||
            Window.GetWindow(this) is not MainWindow ownerWindow)
        {
            return;
        }

        var draft = viewModel.CreateDuplicateTransactionDraft(transaction);
        ownerWindow.OpenAddNewTransactionPopup(draft);
    }

    private void OpenTransactionTagPopup(LedgerTransactionItemVM transaction)
    {
        if (transaction.Kind != LedgerTransactionKind.Expense)
            return;

        transaction.IsTagPopupOpen = false;
        _ = Dispatcher.BeginInvoke(() => transaction.IsTagPopupOpen = true, DispatcherPriority.Input);
    }

    private void OpenTransactionAccountPopup(LedgerTransactionItemVM transaction)
    {
        transaction.IsAccountPopupOpen = false;
        _ = Dispatcher.BeginInvoke(() => transaction.IsAccountPopupOpen = true, DispatcherPriority.Input);
    }

    private async void OnFilterDropDownClosed(object sender, EventArgs e)
    {
        if (_suppressNextFilterDropDownClose &&
            sender is ComboBox comboBox &&
            ReferenceEquals(comboBox, _filterDropDownKeepOpenComboBox))
        {
            _filterDropDownKeepOpenComboBox = null;
            _suppressNextFilterDropDownClose = false;
            _ = Dispatcher.BeginInvoke(() =>
            {
                comboBox.IsDropDownOpen = true;
            }, DispatcherPriority.Input);
            return;
        }

        if (DataContext is not LedgerVM viewModel)
            return;

        if (!viewModel.HasPendingFilterChanges)
            return;

        await ShowFilterRefreshToastAsync(viewModel.ApplyFilters);
    }

    private async void OnClearFiltersClick(object sender, RoutedEventArgs e)
    {
        await ClearFiltersAsync();
    }

    public async void ClearFiltersFromShortcutAsync()
    {
        await ClearFiltersAsync();
    }

    private async Task ClearFiltersAsync()
    {
        if (DataContext is not LedgerVM viewModel)
            return;

        await ShowFilterRefreshToastAsync(viewModel.ClearFilters);
    }

    private async void OnExportDataClick(object sender, RoutedEventArgs e)
    {
        await ExportDataAsync();
    }

    private void OnSelectionModeClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is LedgerVM viewModel)
            viewModel.ToggleSelectionModeCommand.Execute(null);
    }

    private void OnToggleVisibleBatchSelectionClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is LedgerVM viewModel)
            viewModel.ToggleVisibleBatchSelectionCommand.Execute(null);
    }

    private void OnBatchSelectionCheckBoxClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is LedgerVM viewModel)
            viewModel.RefreshBatchSelectionState();
    }

    private async void OnApplyBatchUpdatesClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not LedgerVM viewModel)
            return;

        await _dialogService.ShowToastWhileAsync(
            "Updating transactions",
            async () =>
            {
                await viewModel.ApplyBatchTransactionUpdatesCommand.ExecuteAsync(null);
                await Dispatcher.InvokeAsync(
                    () => LedgerTransactionsList.Items.Refresh(),
                    DispatcherPriority.ContextIdle);
            },
            Window.GetWindow(this));
    }

    private async void OnRemoveSelectedTransactionsClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not LedgerVM viewModel)
        {
            ResetBatchActionToggle(sender, null);
            return;
        }

        if (!viewModel.HasSelectedVisibleTransactions)
        {
            ResetBatchActionToggle(sender, viewModel);
            return;
        }

        var count = viewModel.TransactionsView.Cast<LedgerTransactionItemVM>()
            .Count(transaction => transaction.IsSelectedForBatch);
        var result = FluxoMessageBox.Show(
            Window.GetWindow(this),
            $"Delete {count} selected transaction{(count == 1 ? string.Empty : "s")}?",
            "Ledger",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            ResetBatchActionToggle(sender, viewModel);
            return;
        }

        await viewModel.RemoveSelectedTransactionsCommand.ExecuteAsync(null);
        ResetBatchActionToggle(sender, viewModel);
    }

    private static void ResetBatchActionToggle(object sender, LedgerVM? viewModel)
    {
        if (sender is ToggleButton toggleButton && viewModel is not null)
            toggleButton.IsChecked = viewModel.AreAllVisibleTransactionsSelected;
    }

    public async void ExportDataFromShortcutAsync()
    {
        await ExportDataAsync();
    }

    private async Task ExportDataAsync()
    {
        if (DataContext is not LedgerVM viewModel || !viewModel.HasVisibleTransactions)
            return;

        var transactions = viewModel.GetVisibleTransactionsForExport();
        if (transactions.Count == 0)
            return;

        var dialog = new SaveFileDialog
        {
            AddExtension = true,
            DefaultExt = ".csv",
            FileName = LedgerCsvExport.BuildFileName(DateTime.Now),
            Filter = "CSV files (*.csv)|*.csv"
        };

        var owner = Window.GetWindow(this);
        if (dialog.ShowDialog(owner) != true)
            return;

        await _dialogService.ShowToastWhileAsync(
            "Exporting ledger data...",
            async () =>
            {
                var bytes = LedgerCsvExport.BuildBytes(transactions);
                await File.WriteAllBytesAsync(dialog.FileName, bytes);
            },
            owner);
    }

    private Task ShowFilterRefreshToastAsync(Action refreshAction)
    {
        return _dialogService.ShowToastWhileAsync(
            "Loading data...",
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

        var direction = viewModel.AmountSortDirection == LedgerAmountSortDirection.Descending
            ? LedgerAmountSortDirection.Ascending
            : LedgerAmountSortDirection.Descending;
        await ApplyAmountSortDirectionAsync(direction);
    }

    public async void ApplyAmountSortDirectionFromShortcutAsync(LedgerAmountSortDirection direction)
    {
        await ApplyAmountSortDirectionAsync(direction);
    }

    private async Task ApplyAmountSortDirectionAsync(LedgerAmountSortDirection direction)
    {
        if (DataContext is not LedgerVM viewModel)
            return;

        await _dialogService.ShowToastWhileAsync(
            "Ordering...",
            async () =>
            {
                await Dispatcher.InvokeAsync(
                    () =>
                    {
                        viewModel.AmountSortDirection = direction;
                    },
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

}
