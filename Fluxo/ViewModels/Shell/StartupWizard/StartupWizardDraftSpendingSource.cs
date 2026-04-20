using Fluxo.Core.Enums;

namespace Fluxo.ViewModels.Shell.StartupWizard;

public sealed record StartupWizardDraftSpendingSource(
    int Id,
    string Name,
    SpendingSourceType SpendingSourceType,
    decimal Balance,
    decimal SpentAmount,
    decimal AccountLimit,
    int? MonthlyDueDate,
    int? DeductSource,
    decimal? InterestRate,
    bool ShowOnUi,
    bool IsEnabled);
