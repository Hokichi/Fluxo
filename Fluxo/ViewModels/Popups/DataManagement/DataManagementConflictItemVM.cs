using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Services;

namespace Fluxo.ViewModels.Popups.DataManagement;

public partial class DataManagementConflictItemVM(UserBackupConflict conflict) : ObservableObject
{
    [ObservableProperty] private DataManagementConflictDecision _decision =
        conflict.EntityKind == DataManagementEntityKind.Tags
            ? DataManagementConflictDecision.Ignore
            : DataManagementConflictDecision.Replace;

    public string ConflictKey { get; } = conflict.ConflictKey;
    public DataManagementEntityKind EntityKind { get; } = conflict.EntityKind;
    public string Name { get; } = conflict.Name;
    public bool IsTag => EntityKind == DataManagementEntityKind.Tags;

    public bool IsReplaceSelected
    {
        get => Decision == DataManagementConflictDecision.Replace;
        set
        {
            if (value)
                Decision = DataManagementConflictDecision.Replace;
        }
    }

    public bool IsAppendSelected
    {
        get => Decision == DataManagementConflictDecision.Append;
        set
        {
            if (value)
                Decision = DataManagementConflictDecision.Append;
        }
    }

    public bool IsIgnoreSelected
    {
        get => Decision == DataManagementConflictDecision.Ignore;
        set
        {
            if (value)
                Decision = DataManagementConflictDecision.Ignore;
        }
    }

    partial void OnDecisionChanged(DataManagementConflictDecision value)
    {
        OnPropertyChanged(nameof(IsReplaceSelected));
        OnPropertyChanged(nameof(IsAppendSelected));
        OnPropertyChanged(nameof(IsIgnoreSelected));
    }
}
