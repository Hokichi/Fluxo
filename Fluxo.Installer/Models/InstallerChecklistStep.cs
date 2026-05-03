using CommunityToolkit.Mvvm.ComponentModel;

namespace Fluxo.Installer.Models;

public partial class InstallerChecklistStep(string label) : ObservableObject
{
    public string Label => label;

    [ObservableProperty]
    private ChecklistStepState state = ChecklistStepState.Pending;
}
