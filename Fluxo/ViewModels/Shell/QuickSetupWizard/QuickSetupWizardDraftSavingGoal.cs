using Fluxo.Core.Enums;

namespace Fluxo.ViewModels.Shell.QuickSetupWizard;

public sealed record QuickSetupWizardDraftSavingGoal(
    int Id,
    string Name,
    decimal TargetAmount,
    decimal CurrentAmount,
    DateTime? SavingEndDate,
    RecurringPeriod RecurringPeriod);
