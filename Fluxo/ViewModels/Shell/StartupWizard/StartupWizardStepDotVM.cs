using CommunityToolkit.Mvvm.ComponentModel;

namespace Fluxo.ViewModels.Shell.StartupWizard;

public sealed partial class StartupWizardStepDotVM : ObservableObject
{
    [ObservableProperty] private bool _isActive;

    public int StepIndex { get; }

    public StartupWizardStepDotVM(int stepIndex, bool isActive)
    {
        StepIndex = stepIndex;
        _isActive = isActive;
    }
}

