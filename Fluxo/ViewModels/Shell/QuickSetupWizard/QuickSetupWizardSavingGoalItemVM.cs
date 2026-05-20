using Fluxo.Core.Entities;
using Fluxo.Core.Enums;

namespace Fluxo.ViewModels.Shell.QuickSetupWizard;

public sealed record QuickSetupWizardSavingGoalItemVM(
    int Id,
    string Name,
    decimal CurrentAmount,
    decimal TargetAmount,
    DateTime? SavingEndDate,
    RecurringPeriod RecurringPeriod)
{
    public QuickSetupWizardSavingGoalItemVM(SavingGoal goal) : this(
        goal.Id,
        goal.Name,
        goal.CurrentAmount,
        goal.TargetAmount,
        goal.SavingEndDate,
        goal.RecurringPeriod)
    {
    }

    public QuickSetupWizardSavingGoalItemVM(QuickSetupWizardDraftSavingGoal goal) : this(
        goal.Id,
        goal.Name,
        goal.CurrentAmount,
        goal.TargetAmount,
        goal.SavingEndDate,
        goal.RecurringPeriod)
    {
    }
}
