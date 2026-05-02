using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.ViewModels.Entities;

namespace Fluxo.ViewModels.Popups;

public partial class NotificationChecklistActionItemVM : ObservableObject
{
    [ObservableProperty] private int _entityId;
    [ObservableProperty] private string _label = string.Empty;
    [ObservableProperty] private NotificationChecklistItemActionType _selectedAction = NotificationChecklistItemActionType.Ignore;
    [ObservableProperty] private int? _selectedSourceId;
    [ObservableProperty] private bool _requiresSourceSelection;

    public ObservableCollection<SpendingSourceVM> AvailableSources { get; } = [];

    public bool IsIgnoreSelected
    {
        get => SelectedAction == NotificationChecklistItemActionType.Ignore;
        set
        {
            if (value)
                SelectedAction = NotificationChecklistItemActionType.Ignore;
        }
    }

    public bool IsPaidSelected
    {
        get => SelectedAction == NotificationChecklistItemActionType.Paid;
        set
        {
            if (value)
                SelectedAction = NotificationChecklistItemActionType.Paid;
        }
    }

    public bool IsProcessSelected
    {
        get => SelectedAction == NotificationChecklistItemActionType.Process;
        set
        {
            if (value)
                SelectedAction = NotificationChecklistItemActionType.Process;
        }
    }

    public bool ShowSourceSelector =>
        RequiresSourceSelection && SelectedAction != NotificationChecklistItemActionType.Ignore;

    public bool IsSelected
    {
        get => SelectedAction != NotificationChecklistItemActionType.Ignore;
        set => SelectedAction = value
            ? NotificationChecklistItemActionType.Process
            : NotificationChecklistItemActionType.Ignore;
    }

    partial void OnSelectedActionChanged(NotificationChecklistItemActionType value)
    {
        OnPropertyChanged(nameof(IsIgnoreSelected));
        OnPropertyChanged(nameof(IsPaidSelected));
        OnPropertyChanged(nameof(IsProcessSelected));
        OnPropertyChanged(nameof(ShowSourceSelector));
        OnPropertyChanged(nameof(IsSelected));
    }

    partial void OnRequiresSourceSelectionChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowSourceSelector));
    }
}
