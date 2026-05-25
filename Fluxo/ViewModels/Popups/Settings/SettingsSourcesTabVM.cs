using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Resources.Resources.Messages;
using Fluxo.Services.History;
using Fluxo.Services.Logging;
using Fluxo.ViewModels.Popups;
using Fluxo.ViewModels.Popups.Helpers;
using Fluxo.ViewModels.Shell;
using MainVM = Fluxo.ViewModels.Shell.Main.MainVM;

namespace Fluxo.ViewModels.Popups.Settings;

public partial class SettingsSourcesTabVM : ObservableObject
{
    private const int PageSize = 25;

    private readonly MainVM _mainViewModel;
    private readonly IMessenger _messenger;
    private readonly IAppDataService _appData;
    private readonly HashSet<SettingsSpendingSourceItemVM> _spendingSourcesVisibleWindow = [];
    private int _visibleSpendingSourceCount = PageSize;

    [ObservableProperty] private bool _isSpendingSourceChecksEnabled;
    [ObservableProperty] private bool _hasMoreItems;
    [ObservableProperty] private bool _isLoading;

    public SettingsSourcesTabVM(MainVM mainViewModel, IAppDataService appData, IMessenger? messenger = null)
    {
        _mainViewModel = mainViewModel;
        _appData = appData;
        _messenger = messenger ?? WeakReferenceMessenger.Default;

        SpendingSourcesView = CollectionViewSource.GetDefaultView(SpendingSources);
        SpendingSourcesView.Filter = FilterSpendingSource;
    }

    public ObservableCollection<SettingsSpendingSourceItemVM> SpendingSources { get; } = [];
    public ICollectionView SpendingSourcesView { get; }
    public bool AreAllSpendingSourcesChecked => SpendingSources.Count > 0 && SpendingSources.All(item => item.IsChecked);
    public bool HasSpendingSources => SpendingSources.Count > 0;

    public bool ShowSpendingSourceHideActionButton =>
        IsSpendingSourceChecksEnabled && ShouldShowHideAction(SpendingSources);
    public bool ShowSpendingSourceUnhideActionButton =>
        IsSpendingSourceChecksEnabled && !ShouldShowHideAction(SpendingSources);
    public bool ShowSpendingSourceDisableActionButton =>
        IsSpendingSourceChecksEnabled && ShouldShowDisableAction(SpendingSources);
    public bool ShowSpendingSourceEnableActionButton =>
        IsSpendingSourceChecksEnabled && !ShouldShowDisableAction(SpendingSources);
    public bool ShowSpendingSourceCheckAllButton => IsSpendingSourceChecksEnabled && !AreAllSpendingSourcesChecked;
    public bool ShowSpendingSourceUncheckAllButton => IsSpendingSourceChecksEnabled && AreAllSpendingSourcesChecked;
    public bool ShowSpendingSourceEnableChecksButton => !IsSpendingSourceChecksEnabled && HasSpendingSources;

    public async Task LoadAsync()
    {
        await RefreshSpendingSourcesAsync(resetPagination: true);
        IsSpendingSourceChecksEnabled = false;
    }

    public AddSpendingSourceVM CreateAddSpendingSourceViewModel()
    {
        return new AddSpendingSourceVM(_mainViewModel, _appData);
    }

    public SpendingSourceDetailVM CreateSpendingSourceDetailViewModel(int spendingSourceId)
    {
        return new SpendingSourceDetailVM(_mainViewModel, spendingSourceId, _appData);
    }

    public async Task OpenAddSpendingSourceAsync()
    {
        _messenger.Send(new SettingsDialogRequestedMessage(
            new SettingsDialogRequest(
                SettingsDialogRequestType.AddSpendingSource,
                CreateAddSpendingSourceViewModel())));
        await RefreshSpendingSourcesAsync();
        _messenger.Send(new SettingsDataChangedMessage(SettingsDataChangedScope.SpendingSources));
    }

    public async Task OpenSpendingSourceDetailAsync(int spendingSourceId)
    {
        _messenger.Send(new SettingsDialogRequestedMessage(
            new SettingsDialogRequest(
                SettingsDialogRequestType.SpendingSourceDetail,
                CreateSpendingSourceDetailViewModel(spendingSourceId))));
        await RefreshSpendingSourcesAsync(keepVisibleItemId: spendingSourceId);
        _messenger.Send(new SettingsDataChangedMessage(SettingsDataChangedScope.SpendingSources));
        SelectSingleItem(spendingSourceId);
    }

    public Task<string> BuildDeleteConfirmationMessageAsync(
        int spendingSourceId,
        string? fallbackSourceName = null,
        CancellationToken cancellationToken = default)
    {
        return SpendingSourceDeletionConfirmationHelper.BuildDeleteConfirmationMessageAsync(
            _appData,
            spendingSourceId,
            fallbackSourceName,
            cancellationToken);
    }

    public void ClearSelections()
    {
        SetSelections(false);
    }

    public void SetSelections(bool isChecked)
    {
        foreach (var item in SpendingSources)
            item.IsChecked = isChecked;
    }

    public bool ShouldWarnBeforeApplyingToAll(SettingsBatchAction action)
    {
        if (action is not (SettingsBatchAction.Hide or SettingsBatchAction.Disable))
            return false;

        if (SpendingSources.Count == 0)
            return false;

        var selectedCount = SpendingSources.Count(item => item.IsChecked);
        return selectedCount == SpendingSources.Count;
    }

    public async Task<SettingsOperationResult> ExecuteActionAsync(
        SettingsBatchAction action,
        IReadOnlyCollection<int>? selectedIdsOverride = null)
    {
        var selectedIds = SettingsShared.NormalizeSelectionIds(selectedIdsOverride, SpendingSources.Select(item => item.Id),
            SpendingSources.Where(item => item.IsChecked).Select(item => item.Id));
        if (selectedIds.Length == 0)
            return SettingsOperationResult.Failure("Select at least one spending source first.");

        var actions = new List<ILogMemoryAction>();

        try
        {
            switch (action)
            {
                case SettingsBatchAction.Delete:
                    var allExpenseLogs = await _appData.GetExpenseLogsAsync();
                    var expenseLogsBySourceId = allExpenseLogs
                        .GroupBy(log => log.SpendingSourceId)
                        .ToDictionary(group => group.Key, group => (IReadOnlyList<ExpenseLog>)group.ToList());
                    var allIncomeLogs = await _appData.GetIncomeLogsAsync();
                    var incomeLogsBySourceId = allIncomeLogs
                        .GroupBy(log => log.SpendingSourceId)
                        .ToDictionary(group => group.Key, group => (IReadOnlyList<IncomeLog>)group.ToList());

                    foreach (var selectedId in selectedIds)
                    {
                        var spendingSource = await _appData.GetSpendingSourceByIdAsync(selectedId);
                        if (spendingSource is null)
                            continue;

                        if (expenseLogsBySourceId.TryGetValue(selectedId, out var expenseLogs))
                            foreach (var expenseLog in expenseLogs)
                                _appData.RemoveExpenseLog(expenseLog);

                        if (incomeLogsBySourceId.TryGetValue(selectedId, out var incomeLogs))
                            foreach (var incomeLog in incomeLogs)
                                _appData.RemoveIncomeLog(incomeLog);

                        var snapshot = SpendingSourceMemorySnapshot.Create(spendingSource);
                        _appData.RemoveSpendingSource(spendingSource);
                        actions.Add(new DeleteSpendingSourceMemoryAction(snapshot));
                    }

                    break;

                case SettingsBatchAction.Hide:
                case SettingsBatchAction.Unhide:
                case SettingsBatchAction.Disable:
                case SettingsBatchAction.Enable:
                    foreach (var selectedId in selectedIds)
                    {
                        var spendingSource = await _appData.GetSpendingSourceByIdAsync(selectedId);
                        if (spendingSource is null)
                            continue;

                        var beforeSnapshot = SpendingSourceMemorySnapshot.Create(spendingSource);
                        var updated = false;

                        switch (action)
                        {
                            case SettingsBatchAction.Hide when spendingSource.ShowOnUI:
                                spendingSource.ShowOnUI = false;
                                updated = true;
                                break;
                            case SettingsBatchAction.Unhide when !spendingSource.ShowOnUI:
                                spendingSource.ShowOnUI = true;
                                updated = true;
                                break;
                            case SettingsBatchAction.Disable when spendingSource.IsEnabled:
                                spendingSource.IsEnabled = false;
                                updated = true;
                                break;
                            case SettingsBatchAction.Enable when !spendingSource.IsEnabled:
                                spendingSource.IsEnabled = true;
                                updated = true;
                                break;
                        }

                        if (!updated)
                            continue;

                        _appData.UpdateSpendingSource(spendingSource);
                        var afterSnapshot = SpendingSourceMemorySnapshot.Create(spendingSource);
                        actions.Add(new EditSpendingSourceMemoryAction(beforeSnapshot, afterSnapshot));
                    }

                    break;
            }

            if (actions.Count == 0)
                return SettingsOperationResult.Failure("Nothing changed for the selected spending sources.");

            await _appData.SaveChangesAsync();
            SettingsShared.RecordActions(actions, _messenger);
            _messenger.Send(new DashboardDataInvalidatedMessage(
                DashboardDataInvalidationScope.Budget | DashboardDataInvalidationScope.Notifications));
            await _mainViewModel.ReloadCurrentDataAsync();
            await RefreshSpendingSourcesAsync();
            _messenger.Send(new SettingsDataChangedMessage(SettingsDataChangedScope.SpendingSources));

            return SettingsOperationResult.Success();
        }
        catch (Exception exception)
        {
            FluxoLogManager.LogError(exception, "Unable to update selected spending sources from settings.");
            return SettingsOperationResult.Failure(
                FluxoLogManager.CreateFailureMessage("update selected spending sources"));
        }
    }

    public Task<SettingsOperationResult> ExecuteItemActionAsync(int itemId, SettingsBatchAction action)
    {
        return ExecuteActionAsync(action, [itemId]);
    }

    public void SelectSingleItem(int itemId)
    {
        if (EnsureItemVisible(itemId))
            RefreshSpendingSourcesView();

        foreach (var item in SpendingSources)
            item.IsSelected = item.Id == itemId;
    }

    public async Task RefreshSpendingSourcesAsync(bool resetPagination = false, int? keepVisibleItemId = null)
    {
        SettingsShared.ReplaceCollection(SpendingSources, (await _appData.GetSpendingSourcesAsync())
            .OrderByDescending(source => source.ShowOnUI)
            .ThenBy(source => source.Name)
            .Select(source => new SettingsSpendingSourceItemVM(source)));

        AttachSelectableItemHandlers(SpendingSources);
        if (resetPagination)
            ResetPaginationWindow();
        else
            IsLoading = false;

        if (keepVisibleItemId is int itemId)
            EnsureItemVisible(itemId);

        RefreshSpendingSourcesView();
        OnPropertyChanged(nameof(HasSpendingSources));
        OnSelectionStateChanged();
    }

    partial void OnIsSpendingSourceChecksEnabledChanged(bool value)
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
            _visibleSpendingSourceCount += PageSize;
            RefreshSpendingSourcesView();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void AttachSelectableItemHandlers(IEnumerable<SettingsSpendingSourceItemVM> items)
    {
        foreach (var item in items)
        {
            item.PropertyChanged -= OnSelectableItemPropertyChanged;
            item.PropertyChanged += OnSelectableItemPropertyChanged;
        }
    }

    private void OnSelectableItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsSpendingSourceItemVM.IsChecked))
            OnSelectionStateChanged();
    }

    private void OnSelectionStateChanged()
    {
        OnPropertyChanged(nameof(AreAllSpendingSourcesChecked));
        OnPropertyChanged(nameof(ShowSpendingSourceHideActionButton));
        OnPropertyChanged(nameof(ShowSpendingSourceUnhideActionButton));
        OnPropertyChanged(nameof(ShowSpendingSourceDisableActionButton));
        OnPropertyChanged(nameof(ShowSpendingSourceEnableActionButton));
        OnPropertyChanged(nameof(ShowSpendingSourceCheckAllButton));
        OnPropertyChanged(nameof(ShowSpendingSourceUncheckAllButton));
        OnPropertyChanged(nameof(ShowSpendingSourceEnableChecksButton));
    }

    private bool CanLoadMore()
    {
        return HasMoreItems && !IsLoading;
    }

    private bool FilterSpendingSource(object item)
    {
        return item is SettingsSpendingSourceItemVM spendingSource &&
               _spendingSourcesVisibleWindow.Contains(spendingSource);
    }

    private void ResetPaginationWindow()
    {
        _visibleSpendingSourceCount = PageSize;
        IsLoading = false;
    }

    private bool EnsureItemVisible(int itemId)
    {
        var index = -1;
        for (var i = 0; i < SpendingSources.Count; i++)
            if (SpendingSources[i].Id == itemId)
            {
                index = i;
                break;
            }

        if (index < 0)
            return false;

        var requiredVisibleCount = ((index / PageSize) + 1) * PageSize;
        if (requiredVisibleCount <= _visibleSpendingSourceCount)
            return false;

        _visibleSpendingSourceCount = requiredVisibleCount;
        return true;
    }

    private void RefreshSpendingSourcesView()
    {
        RecomputeVisibleWindow();
        SpendingSourcesView.Refresh();
    }

    private void RecomputeVisibleWindow()
    {
        _spendingSourcesVisibleWindow.Clear();

        foreach (var spendingSource in SpendingSources.Take(_visibleSpendingSourceCount))
            _spendingSourcesVisibleWindow.Add(spendingSource);

        HasMoreItems = SpendingSources.Count > _visibleSpendingSourceCount;
    }

    private static bool ShouldShowHideAction(IReadOnlyCollection<SettingsSpendingSourceItemVM> items)
    {
        if (items.Count == 0)
            return true;

        var scopedItems = GetScopedItems(items);
        return scopedItems.Any(item => !item.IsHidden);
    }

    private static bool ShouldShowDisableAction(IReadOnlyCollection<SettingsSpendingSourceItemVM> items)
    {
        if (items.Count == 0)
            return true;

        var scopedItems = GetScopedItems(items);
        return scopedItems.Any(item => item.IsEnabled);
    }

    private static IReadOnlyList<SettingsSpendingSourceItemVM> GetScopedItems(
        IReadOnlyCollection<SettingsSpendingSourceItemVM> items)
    {
        var selectedItems = items.Where(item => item.IsChecked).ToArray();
        return selectedItems.Length > 0 ? selectedItems : items.ToArray();
    }
}

