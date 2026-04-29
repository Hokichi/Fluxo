using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Fluxo.ViewModels.Popups;

public partial class NotificationChecklistActionVM : ObservableObject
{
    private readonly HashSet<NotificationChecklistActionItemVM> _trackedItems = [];

    public NotificationChecklistActionVM(IEnumerable<NotificationChecklistActionItemVM>? items = null)
    {
        Items.CollectionChanged += OnItemsCollectionChanged;

        if (items is null)
            return;

        foreach (var item in items)
            Items.Add(item);
    }

    [ObservableProperty] private bool _didProceed;

    public ObservableCollection<NotificationChecklistActionItemVM> Items { get; } = [];

    public IReadOnlyList<NotificationChecklistActionItemVM> SelectedItems =>
        Items.Where(item => item.SelectedAction != NotificationChecklistItemActionType.Ignore).ToList();

    public IReadOnlyList<NotificationChecklistActionDecision> ActionDecisions =>
        Items
            .Where(IsActionExecutable)
            .Select(item => new NotificationChecklistActionDecision(
                item.EntityId,
                item.SelectedAction,
                item.SelectedSourceId))
            .ToList();

    public bool CanProceed => Items.Any(IsActionExecutable);

    [RelayCommand(CanExecute = nameof(CanProceed))]
    private void Proceed()
    {
        DidProceed = true;
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
            !string.Equals(e.PropertyName, nameof(NotificationChecklistActionItemVM.RequiresSourceSelection), StringComparison.Ordinal))
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

        if (item.RequiresSourceSelection && item.SelectedSourceId is null)
            return false;

        return true;
    }
}
