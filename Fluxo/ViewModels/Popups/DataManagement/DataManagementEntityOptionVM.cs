using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.Enums;

namespace Fluxo.ViewModels.Popups.DataManagement;

public partial class DataManagementEntityOptionVM(
    DataManagementEntityKind entityKind,
    string title) : ObservableObject
{
    [ObservableProperty] private bool _isChecked = true;
    [ObservableProperty] private bool _isEnabled = true;

    public DataManagementEntityKind EntityKind { get; } = entityKind;
    public string Title { get; } = title;
}
