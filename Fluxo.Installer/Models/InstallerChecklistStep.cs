using CommunityToolkit.Mvvm.ComponentModel;

namespace Fluxo.Installer.Models;

public partial class InstallerChecklistStep(string initialLabel) : ObservableObject
{
    [ObservableProperty]
    private string label = initialLabel;

    [ObservableProperty]
    private ChecklistStepState state = ChecklistStepState.Pending;
}
