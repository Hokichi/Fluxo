using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
using Fluxo.Resources.Messages;
using Fluxo.Services.History;
using Fluxo.ViewModels.Popups;
using Fluxo.ViewModels.Shell;

namespace Fluxo.ViewModels.Popups.Settings;

public partial class SettingsSourcesTabVM : ObservableObject
{
    private readonly MainVM _mainViewModel;
    private readonly IMessenger _messenger;
    private readonly IUnitOfWork _unitOfWork;

    [ObservableProperty] private bool _isSpendingSourceChecksEnabled;

    public SettingsSourcesTabVM(MainVM mainViewModel, IUnitOfWork unitOfWork, IMessenger? messenger = null)
    {
        _mainViewModel = mainViewModel;
        _unitOfWork = unitOfWork;
        _messenger = messenger ?? WeakReferenceMessenger.Default;
    }

    public ObservableCollection<SettingsSpendingSourceItemVM> SpendingSources { get; } = [];
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
        await RefreshSpendingSourcesAsync();
        IsSpendingSourceChecksEnabled = false;
    }

    public AddSpendingSourceVM CreateAddSpendingSourceViewModel()
    {
        return new AddSpendingSourceVM(_mainViewModel, _unitOfWork);
    }

    public SpendingSourceDetailVM CreateSpendingSourceDetailViewModel(int spendingSourceId)
    {
        return new SpendingSourceDetailVM(_mainViewModel, spendingSourceId, _unitOfWork);
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
                    var allExpenseLogs = await _unitOfWork.ExpenseLogs.GetAllAsync();
                    var activeExpenseSourceIds = allExpenseLogs.Where(log => !log.IsForDeletion)
                        .Select(log => log.SpendingSourceId).ToHashSet();
                    var allIncomeLogs = await _unitOfWork.IncomeLogs.GetAllAsync();
                    var incomeSourceIds = allIncomeLogs.Select(log => log.SpendingSourceId).ToHashSet();

                    foreach (var selectedId in selectedIds)
                    {
                        var spendingSource = await _unitOfWork.SpendingSources.GetByIdAsync(selectedId);
                        if (spendingSource is null)
                            continue;

                        if (activeExpenseSourceIds.Contains(selectedId) || incomeSourceIds.Contains(selectedId))
                            return SettingsOperationResult.Failure(
                                $"{spendingSource.Name} still has activity, so it can't be deleted yet.");

                        var snapshot = SpendingSourceMemorySnapshot.Create(spendingSource);
                        _unitOfWork.SpendingSources.Remove(spendingSource);
                        actions.Add(new DeleteSpendingSourceMemoryAction(snapshot));
                    }

                    break;

                case SettingsBatchAction.Hide:
                case SettingsBatchAction.Unhide:
                case SettingsBatchAction.Disable:
                case SettingsBatchAction.Enable:
                    foreach (var selectedId in selectedIds)
                    {
                        var spendingSource = await _unitOfWork.SpendingSources.GetByIdAsync(selectedId);
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

                        _unitOfWork.SpendingSources.Update(spendingSource);
                        var afterSnapshot = SpendingSourceMemorySnapshot.Create(spendingSource);
                        actions.Add(new EditSpendingSourceMemoryAction(beforeSnapshot, afterSnapshot));
                    }

                    break;
            }

            if (actions.Count == 0)
                return SettingsOperationResult.Failure("Nothing changed for the selected spending sources.");

            await _unitOfWork.SaveChangesAsync();
            SettingsShared.RecordActions(actions, _messenger);
            _messenger.Send(new SettingsDataChangedMessage(SettingsDataChangedScope.SpendingSources));
            _messenger.Send(new DashboardDataInvalidatedMessage(
                DashboardDataInvalidationScope.Budget | DashboardDataInvalidationScope.Notifications));
            await _mainViewModel.ReloadCurrentDataAsync();
            await RefreshSpendingSourcesAsync();

            return SettingsOperationResult.Success();
        }
        catch (Exception exception)
        {
            return SettingsOperationResult.Failure(
                $"Unable to update the selected spending sources.\n\n{exception.Message}");
        }
    }

    public Task<SettingsOperationResult> ExecuteItemActionAsync(int itemId, SettingsBatchAction action)
    {
        return ExecuteActionAsync(action, [itemId]);
    }

    public void SelectSingleItem(int itemId)
    {
        foreach (var item in SpendingSources)
            item.IsSelected = item.Id == itemId;
    }

    public async Task RefreshSpendingSourcesAsync()
    {
        SettingsShared.ReplaceCollection(SpendingSources, (await _unitOfWork.SpendingSources.GetAllAsync())
            .OrderByDescending(source => source.ShowOnUI)
            .ThenBy(source => source.Name)
            .Select(source => new SettingsSpendingSourceItemVM(source)));

        AttachSelectableItemHandlers(SpendingSources);
        OnPropertyChanged(nameof(HasSpendingSources));
        OnSelectionStateChanged();
    }

    partial void OnIsSpendingSourceChecksEnabledChanged(bool value)
    {
        OnSelectionStateChanged();
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
