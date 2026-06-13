using Fluxo.Core.Enums;

namespace Fluxo.ViewModels.Shell.QuickSetupWizard;

public sealed record QuickSetupWizardDraftSpendingSource(
    int Id,
    string Name,
    SpendingSourceType SpendingSourceType,
    decimal Balance,
    decimal SpentAmount,
    decimal AccountLimit,
    decimal MaximumSpending,
    decimal? MinimumPayment,
    int? MonthlyDueDate,
    int? DeductSource,
    decimal? InterestRate,
    bool PinnedOnUi,
    bool IsEnabled);
