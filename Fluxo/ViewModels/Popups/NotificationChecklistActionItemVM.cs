using CommunityToolkit.Mvvm.ComponentModel;

namespace Fluxo.ViewModels.Popups;

public partial class NotificationChecklistActionItemVM : ObservableObject
{
    [ObservableProperty] private int _entityId;
    [ObservableProperty] private string _label = string.Empty;
    [ObservableProperty] private bool _isSelected;
}
