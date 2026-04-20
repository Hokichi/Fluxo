namespace Fluxo.ViewModels.Shell.StartupWizard;

public sealed record StartupWizardDraftSavingGoal(
    int Id,
    string Name,
    decimal TargetAmount,
    decimal CurrentAmount,
    DateTime SavingEndDate);
