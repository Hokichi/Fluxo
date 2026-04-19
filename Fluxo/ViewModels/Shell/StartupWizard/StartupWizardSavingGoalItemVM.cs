using Fluxo.Core.Entities;

namespace Fluxo.ViewModels.Shell.StartupWizard;

public sealed record StartupWizardSavingGoalItemVM(
    int Id,
    string Name,
    decimal CurrentAmount,
    decimal TargetAmount,
    DateTime SavingEndDate)
{
    public StartupWizardSavingGoalItemVM(SavingGoal goal) : this(
        goal.Id,
        goal.Name,
        goal.CurrentAmount,
        goal.TargetAmount,
        goal.SavingEndDate)
    {
    }
}

