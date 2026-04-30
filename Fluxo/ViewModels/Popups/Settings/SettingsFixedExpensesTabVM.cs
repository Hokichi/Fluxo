using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Resources.Messages;
using Fluxo.Services.History;
using Fluxo.ViewModels.Popups;
using Fluxo.ViewModels.Shell;
using MainVM = Fluxo.ViewModels.Shell.Main.MainVM;

namespace Fluxo.ViewModels.Popups.Settings;

public partial class SettingsFixedExpensesTabVM : ObservableObject
{
    private const int PageSize = 25;

    private readonly MainVM _mainViewModel;
    private readonly IMessenger _messenger;
    private readonly IAppDataService _appData;
    private readonly HashSet<SettingsFixedExpenseItemVM> _fixedExpensesVisibleWindow = [];
    private int _visibleFixedExpenseCount = PageSize;

    [ObservableProperty] private bool _isFixedExpenseChecksEnabled;
    [ObservableProperty] private bool _hasMoreItems;
    [ObservableProperty] private bool _isLoading;

    public SettingsFixedExpensesTabVM(MainVM mainViewModel, IAppDataService appData, IMessenger? messenger = null)
    {
        _mainViewModel = mainViewModel;
        _appData = appData;
        _messenger = messenger ?? WeakReferenceMessenger.Default;

        FixedExpensesView = CollectionViewSource.GetDefaultView(FixedExpenses);
        FixedExpensesView.Filter = FilterFixedExpense;
    }

    public ObservableCollection<SettingsFixedExpenseItemVM> FixedExpenses { get; } = [];
    public ICollectionView FixedExpensesView { get; }
    public bool AreAllFixedExpensesChecked => FixedExpenses.Count > 0 && FixedExpenses.All(item => item.IsChecked);
    public bool HasFixedExpenses => FixedExpenses.Count > 0;

    public bool ShowFixedExpenseDisableActionButton =>
        IsFixedExpenseChecksEnabled && ShouldShowDisableAction(FixedExpenses);
    public bool ShowFixedExpenseEnableActionButton =>
        IsFixedExpenseChecksEnabled && !ShouldShowDisableAction(FixedExpenses);
    public bool ShowFixedExpenseCheckAllButton => IsFixedExpenseChecksEnabled && !AreAllFixedExpensesChecked;
    public bool ShowFixedExpenseUncheckAllButton => IsFixedExpenseChecksEnabled && AreAllFixedExpensesChecked;
    public bool ShowFixedExpenseEnableChecksButton => !IsFixedExpenseChecksEnabled && HasFixedExpenses;

    public async Task LoadAsync()
    {
        await RefreshFixedExpensesAsync();
        IsFixedExpenseChecksEnabled = false;
    }

    public AddFixedExpenseVM CreateAddFixedExpenseViewModel()
    {
        return new AddFixedExpenseVM(_mainViewModel, _appData);
    }

    public async Task<AddFixedExpenseVM?> CreateEditFixedExpenseViewModelAsync(int fixedExpenseId)
    {
        var expense = await _appData.GetExpenseByIdAsync(fixedExpenseId);
        if (expense is null)
            return null;

        var viewModel = new AddFixedExpenseVM(_mainViewModel, _appData, forceIncludeSpendingSourceId: expense.SpendingSourceId)
        {
            EditingId = expense.Id,
            NameText = expense.Name,
            AmountText = expense.Amount,
            SelectedCategory = expense.ExpenseCategory,
            RecurringDateText = expense.RecurringDate?.ToString(CultureInfo.InvariantCulture) ??
                                string.Empty,
            IsActive = expense.IsActive,
            TagNameText = expense.ExpenseTag?.Name ?? "General"
        };

        if (expense.SpendingSourceId > 0)
        {
            var matchingSource = viewModel.SpendingSources.FirstOrDefault(source => source.Id == expense.SpendingSourceId);
            if (matchingSource is not null)
                viewModel.SelectedSpendingSource = matchingSource;
        }

        return viewModel;
    }

    public async Task OpenAddFixedExpenseAsync()
    {
        _messenger.Send(new SettingsDialogRequestedMessage(
            new SettingsDialogRequest(
                SettingsDialogRequestType.AddFixedExpense,
                CreateAddFixedExpenseViewModel())));
        await RefreshFixedExpensesAsync(resetPagination: false);
    }

    public async Task OpenEditFixedExpenseAsync(int fixedExpenseId)
    {
        var viewModel = await CreateEditFixedExpenseViewModelAsync(fixedExpenseId);
        if (viewModel is null)
            return;

        _messenger.Send(new SettingsDialogRequestedMessage(
            new SettingsDialogRequest(
                SettingsDialogRequestType.AddFixedExpense,
                viewModel)));

        await RefreshFixedExpensesAsync(resetPagination: false);
        SelectSingleItem(fixedExpenseId);
    }

    public void ClearSelections()
    {
        SetSelections(false);
    }

    public void SetSelections(bool isChecked)
    {
        foreach (var item in FixedExpenses)
            item.IsChecked = isChecked;
    }

    public bool ShouldWarnBeforeApplyingToAll(SettingsBatchAction action)
    {
        if (action != SettingsBatchAction.Disable || FixedExpenses.Count == 0)
            return false;

        var selectedCount = FixedExpenses.Count(item => item.IsChecked);
        return selectedCount == FixedExpenses.Count;
    }

    public async Task<SettingsOperationResult> ExecuteActionAsync(
        SettingsBatchAction action,
        IReadOnlyCollection<int>? selectedIdsOverride = null)
    {
        var selectedIds = SettingsShared.NormalizeSelectionIds(selectedIdsOverride, FixedExpenses.Select(item => item.Id),
            FixedExpenses.Where(item => item.IsChecked).Select(item => item.Id));
        var selectedItemIds = selectedIds.ToHashSet();
        var selectedItems = FixedExpenses.Where(item => selectedItemIds.Contains(item.Id)).ToArray();
        if (selectedItems.Length == 0)
            return SettingsOperationResult.Failure("Select at least one fixed expense first.");

        var actions = new List<ILogMemoryAction>();

        try
        {
            switch (action)
            {
                case SettingsBatchAction.Delete:
                    var expenseLogs = await _appData.GetExpenseLogsAsync();

                    foreach (var selectedItem in selectedItems)
                    {
                        if (expenseLogs.Any(log => log.ExpenseId == selectedItem.Id && !log.IsForDeletion))
                            return SettingsOperationResult.Failure(
                                $"{selectedItem.Name} still has logged activity, so it can't be deleted yet.");

                        var expense = await _appData.GetExpenseByIdAsync(selectedItem.Id);
                        if (expense is null)
                            continue;

                        var snapshot = ExpenseMemorySnapshot.Create(expense);
                        _appData.RemoveExpense(expense);
                        actions.Add(new DeleteExpenseMemoryAction(snapshot));
                    }

                    break;

                case SettingsBatchAction.Disable:
                case SettingsBatchAction.Enable:
                    foreach (var selectedItem in selectedItems)
                    {
                        var expense = await _appData.GetExpenseByIdAsync(selectedItem.Id);
                        if (expense is null)
                            continue;

                        var beforeSnapshot = ExpenseMemorySnapshot.Create(expense);
                        var updated = false;

                        if (action == SettingsBatchAction.Disable && expense.IsActive)
                        {
                            expense.IsActive = false;
                            updated = true;
                        }
                        else if (action == SettingsBatchAction.Enable && !expense.IsActive)
                        {
                            expense.IsActive = true;
                            updated = true;
                        }

                        if (!updated)
                            continue;

                        _appData.UpdateExpense(expense);
                        var afterSnapshot = ExpenseMemorySnapshot.Create(expense);
                        actions.Add(new EditExpenseMemoryAction(beforeSnapshot, afterSnapshot));
                    }

                    break;

                case SettingsBatchAction.Hide:
                case SettingsBatchAction.Unhide:
                    return SettingsOperationResult.Failure(
                        "Hide and unhide are no longer supported for fixed expenses.");
            }

            if (actions.Count == 0)
                return SettingsOperationResult.Failure("Nothing changed for the selected fixed expenses.");

            await _appData.SaveChangesAsync();
            SettingsShared.RecordActions(actions, _messenger);
            _messenger.Send(new SettingsDataChangedMessage(SettingsDataChangedScope.FixedExpenses));
            _messenger.Send(new DashboardDataInvalidatedMessage(
                DashboardDataInvalidationScope.Budget | DashboardDataInvalidationScope.Notifications));
            await _mainViewModel.ReloadCurrentDataAsync();
            await RefreshFixedExpensesAsync(resetPagination: false);

            return SettingsOperationResult.Success();
        }
        catch (Exception exception)
        {
            return SettingsOperationResult.Failure(
                $"Unable to update the selected fixed expenses.\n\n{exception.Message}");
        }
    }

    public Task<SettingsOperationResult> ExecuteItemActionAsync(int itemId, SettingsBatchAction action)
    {
        return ExecuteActionAsync(action, [itemId]);
    }

    public void SelectSingleItem(int itemId)
    {
        if (EnsureItemVisible(itemId))
            RefreshFixedExpensesView();

        foreach (var item in FixedExpenses)
            item.IsSelected = item.Id == itemId;
    }

    public async Task RefreshFixedExpensesAsync(bool resetPagination = true)
    {
        SettingsShared.ReplaceCollection(FixedExpenses, (await _appData.GetExpensesAsync())
            .Where(expense => expense.ExpenseKind == ExpenseKind.Fixed)
            .OrderBy(expense => expense.Name)
            .Select(expense => new SettingsFixedExpenseItemVM(expense)));

        AttachSelectableItemHandlers(FixedExpenses);
        if (resetPagination)
            ResetPaginationWindow();
        else
            IsLoading = false;

        RefreshFixedExpensesView();
        OnPropertyChanged(nameof(HasFixedExpenses));
        OnSelectionStateChanged();
    }

    partial void OnIsFixedExpenseChecksEnabledChanged(bool value)
    {
        OnSelectionStateChanged();
    }

    partial void OnHasMoreItemsChanged(bool value)
    {
        LoadMoreCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsLoadingChanged(bool value)
    {
        LoadMoreCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanLoadMore))]
    private void LoadMore()
    {
        if (!CanLoadMore())
            return;

        IsLoading = true;
        try
        {
            _visibleFixedExpenseCount += PageSize;
            RefreshFixedExpensesView();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void AttachSelectableItemHandlers(IEnumerable<SettingsFixedExpenseItemVM> items)
    {
        foreach (var item in items)
        {
            item.PropertyChanged -= OnSelectableItemPropertyChanged;
            item.PropertyChanged += OnSelectableItemPropertyChanged;
        }
    }

    private void OnSelectableItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsFixedExpenseItemVM.IsChecked))
            OnSelectionStateChanged();
    }

    private void OnSelectionStateChanged()
    {
        OnPropertyChanged(nameof(AreAllFixedExpensesChecked));
        OnPropertyChanged(nameof(ShowFixedExpenseDisableActionButton));
        OnPropertyChanged(nameof(ShowFixedExpenseEnableActionButton));
        OnPropertyChanged(nameof(ShowFixedExpenseCheckAllButton));
        OnPropertyChanged(nameof(ShowFixedExpenseUncheckAllButton));
        OnPropertyChanged(nameof(ShowFixedExpenseEnableChecksButton));
    }

    private bool CanLoadMore()
    {
        return HasMoreItems && !IsLoading;
    }

    private bool FilterFixedExpense(object item)
    {
        return item is SettingsFixedExpenseItemVM fixedExpense &&
               _fixedExpensesVisibleWindow.Contains(fixedExpense);
    }

    private void ResetPaginationWindow()
    {
        _visibleFixedExpenseCount = PageSize;
        IsLoading = false;
    }

    private bool EnsureItemVisible(int itemId)
    {
        var index = -1;
        for (var i = 0; i < FixedExpenses.Count; i++)
            if (FixedExpenses[i].Id == itemId)
            {
                index = i;
                break;
            }

        if (index < 0)
            return false;

        var requiredVisibleCount = ((index / PageSize) + 1) * PageSize;
        if (requiredVisibleCount <= _visibleFixedExpenseCount)
            return false;

        _visibleFixedExpenseCount = requiredVisibleCount;
        return true;
    }

    private void RefreshFixedExpensesView()
    {
        RecomputeVisibleWindow();
        FixedExpensesView.Refresh();
    }

    private void RecomputeVisibleWindow()
    {
        _fixedExpensesVisibleWindow.Clear();

        foreach (var fixedExpense in FixedExpenses.Take(_visibleFixedExpenseCount))
            _fixedExpensesVisibleWindow.Add(fixedExpense);

        HasMoreItems = FixedExpenses.Count > _visibleFixedExpenseCount;
    }

    private static bool ShouldShowDisableAction(IReadOnlyCollection<SettingsFixedExpenseItemVM> items)
    {
        if (items.Count == 0)
            return true;

        var scopedItems = GetScopedItems(items);
        return scopedItems.Any(item => item.IsEnabled);
    }

    private static IReadOnlyList<SettingsFixedExpenseItemVM> GetScopedItems(
        IReadOnlyCollection<SettingsFixedExpenseItemVM> items)
    {
        var selectedItems = items.Where(item => item.IsChecked).ToArray();
        return selectedItems.Length > 0 ? selectedItems : items.ToArray();
    }
}

