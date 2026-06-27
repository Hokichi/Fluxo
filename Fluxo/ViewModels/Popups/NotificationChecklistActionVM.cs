using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Services;
using Fluxo.ViewModels.Entities;

namespace Fluxo.ViewModels.Popups;

public partial class NotificationChecklistActionVM : ObservableObject
{
    private IAppDataService? _appData;
    private readonly HashSet<NotificationChecklistActionItemVM> _trackedItems = [];

    public NotificationChecklistActionVM()
        : this(null, null)
    {
    }

    public NotificationChecklistActionVM(IEnumerable<NotificationChecklistActionItemVM>? items = null)
        : this(null, items)
    {
    }

    public NotificationChecklistActionVM(IAppDataService appData)
        : this(appData, null)
    {
    }

    public NotificationChecklistActionVM(IAppDataService? appData, IEnumerable<NotificationChecklistActionItemVM>? items)
    {
        _appData = appData;
        Items.CollectionChanged += OnItemsCollectionChanged;

        if (items is null)
            return;

        foreach (var item in items)
            Items.Add(item);
    }

    [ObservableProperty] private bool _didProceed;

    public ObservableCollection<NotificationChecklistActionItemVM> Items { get; } = [];

    public Func<Task<bool>>? ProcessAsyncCallback { get; set; }

    public IReadOnlyList<NotificationChecklistActionItemVM> SelectedItems =>
        Items.Where(item => item.SelectedAction != NotificationChecklistItemActionType.Ignore).ToList();

    public IReadOnlyList<NotificationChecklistActionDecision> ActionDecisions =>
        Items
            .Where(IsActionExecutable)
            .Select(item => new NotificationChecklistActionDecision(
                item.EntityId,
                item.SelectedAction,
                item.SelectedSourceId,
                item.IsRecurringTransaction ? item.Amount : null,
                item.IsRecurringExpense ? item.SelectedTagId : null,
                item.IsRecurringGoalUpdate ? item.SelectedGoalId : null,
                item.UpdateRecurringAmount))
            .ToList();

    public bool CanProceed => Items.Any(IsActionExecutable);

    public void AttachAppDataService(IAppDataService appData)
    {
        _appData ??= appData;
    }

    public Task<bool> ProcessAsync()
    {
        return ProcessAsyncCallback is null
            ? Task.FromResult(false)
            : ProcessAsyncCallback();
    }

    [RelayCommand(CanExecute = nameof(CanProceed))]
    private void Proceed()
    {
        DidProceed = true;
    }

    public async Task RefreshAvailableTagsAsync(CancellationToken cancellationToken = default)
    {
        if (_appData is null)
            return;

        var tags = (await _appData.GetTagsAsync(cancellationToken))
            .Where(tag => !tag.IsSystemTag)
            .OrderBy(tag => tag.Name)
            .Select(tag => new TagVM
            {
                Id = tag.Id,
                Name = tag.Name,
                HexCode = tag.HexCode,
                IsSystemTag = tag.IsSystemTag,
                SpendingLimit = tag.SpendingLimit
            })
            .ToList();

        foreach (var item in Items)
        {
            var selectedTagId = item.SelectedTagId;
            item.AvailableTags.Clear();
            foreach (var tag in tags)
            {
                item.AvailableTags.Add(new TagVM
                {
                    Id = tag.Id,
                    Name = tag.Name,
                    HexCode = tag.HexCode,
                    IsSystemTag = tag.IsSystemTag,
                    SpendingLimit = tag.SpendingLimit
                });
            }

            if (selectedTagId.HasValue && tags.Any(tag => tag.Id == selectedTagId.Value))
                item.SelectedTagId = selectedTagId;
        }
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var trackedItem in _trackedItems)
                trackedItem.PropertyChanged -= OnChecklistItemPropertyChanged;

            _trackedItems.Clear();
        }
        else if (e.OldItems is not null)
        {
            foreach (var oldItem in e.OldItems.OfType<NotificationChecklistActionItemVM>())
            {
                oldItem.PropertyChanged -= OnChecklistItemPropertyChanged;
                _trackedItems.Remove(oldItem);
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var newItem in e.NewItems.OfType<NotificationChecklistActionItemVM>())
            {
                if (_trackedItems.Add(newItem))
                    newItem.PropertyChanged += OnChecklistItemPropertyChanged;
            }
        }

        OnPropertyChanged(nameof(CanProceed));
        OnPropertyChanged(nameof(SelectedItems));
        OnPropertyChanged(nameof(ActionDecisions));
        ProceedCommand.NotifyCanExecuteChanged();
    }

    private void OnChecklistItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(NotificationChecklistActionItemVM.SelectedAction), StringComparison.Ordinal) &&
            !string.Equals(e.PropertyName, nameof(NotificationChecklistActionItemVM.SelectedSourceId), StringComparison.Ordinal) &&
            !string.Equals(e.PropertyName, nameof(NotificationChecklistActionItemVM.RequiresSourceSelection), StringComparison.Ordinal) &&
            !string.Equals(e.PropertyName, nameof(NotificationChecklistActionItemVM.Amount), StringComparison.Ordinal) &&
            !string.Equals(e.PropertyName, nameof(NotificationChecklistActionItemVM.SelectedTagId), StringComparison.Ordinal) &&
            !string.Equals(e.PropertyName, nameof(NotificationChecklistActionItemVM.SelectedGoalId), StringComparison.Ordinal))
            return;

        OnPropertyChanged(nameof(CanProceed));
        OnPropertyChanged(nameof(SelectedItems));
        OnPropertyChanged(nameof(ActionDecisions));
        ProceedCommand.NotifyCanExecuteChanged();
    }

    private static bool IsActionExecutable(NotificationChecklistActionItemVM item)
    {
        if (item.SelectedAction == NotificationChecklistItemActionType.Ignore)
            return false;

        if (item.SelectedAction != NotificationChecklistItemActionType.Process)
            return true;

        if ((item.RequiresSourceSelection || item.IsRecurringTransaction) && item.SelectedSourceId is null)
            return false;

        if (!item.IsRecurringTransaction)
            return true;

        if (item.Amount <= 0m)
            return false;

        return item.RecurringTransactionType switch
        {
            RecurringTransactionType.Expense => item.SelectedTagId.HasValue,
            RecurringTransactionType.Income => true,
            RecurringTransactionType.GoalUpdate => item.SelectedGoalId.HasValue,
            _ => !item.RequiresSourceSelection || item.SelectedSourceId.HasValue
        };
    }
}
