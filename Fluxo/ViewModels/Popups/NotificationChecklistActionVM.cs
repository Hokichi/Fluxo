using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Fluxo.ViewModels.Popups;

public partial class NotificationChecklistActionVM : ObservableObject
{
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
            .Where(item => item.SelectedAction != NotificationChecklistItemActionType.Ignore)
            .Select(item => new NotificationChecklistActionDecision(
                item.EntityId,
                item.SelectedAction,
                item.SelectedSourceId))
            .ToList();

    public bool CanProceed => Items.Any(item => item.SelectedAction != NotificationChecklistItemActionType.Ignore);

    [RelayCommand(CanExecute = nameof(CanProceed))]
    private void Proceed()
    {
        DidProceed = true;
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var oldItem in e.OldItems.OfType<NotificationChecklistActionItemVM>())
                oldItem.PropertyChanged -= OnChecklistItemPropertyChanged;
        }

        if (e.NewItems is not null)
        {
            foreach (var newItem in e.NewItems.OfType<NotificationChecklistActionItemVM>())
                newItem.PropertyChanged += OnChecklistItemPropertyChanged;
        }

        OnPropertyChanged(nameof(CanProceed));
        OnPropertyChanged(nameof(SelectedItems));
        OnPropertyChanged(nameof(ActionDecisions));
        ProceedCommand.NotifyCanExecuteChanged();
    }

    private void OnChecklistItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(NotificationChecklistActionItemVM.SelectedAction), StringComparison.Ordinal) &&
            !string.Equals(e.PropertyName, nameof(NotificationChecklistActionItemVM.SelectedSourceId), StringComparison.Ordinal))
            return;

        OnPropertyChanged(nameof(CanProceed));
        OnPropertyChanged(nameof(SelectedItems));
        OnPropertyChanged(nameof(ActionDecisions));
        ProceedCommand.NotifyCanExecuteChanged();
    }
}
