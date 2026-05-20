using Fluxo.Core.Entities;
using Fluxo.Core.Enums;

namespace Fluxo.ViewModels.Shell.QuickSetupWizard;

public sealed record QuickSetupWizardSpendingSourceItemVM(
    int Id,
    string Name,
    string TypeLabel,
    decimal PrimaryAmount,
    string PrimaryAmountLabel,
    decimal MaximumSpending,
    decimal? MinimumPayment)
{
    public QuickSetupWizardSpendingSourceItemVM(SpendingSource spendingSource) : this(
        spendingSource.Id,
        spendingSource.Name,
        spendingSource.SpendingSourceType switch
        {
            SpendingSourceType.Credit => "Credit",
            SpendingSourceType.BNPL => "BNPL",
            SpendingSourceType.Checking => "Checking",
            SpendingSourceType.Cash => "Cash",
            SpendingSourceType.Saving => "Savings",
            _ => "Source"
        },
        spendingSource.SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL
            ? spendingSource.SpentAmount
            : spendingSource.Balance,
        spendingSource.SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL
            ? "Spent"
            : "Balance",
        spendingSource.MaximumSpending,
        spendingSource.MinimumPayment)
    {
    }

    public QuickSetupWizardSpendingSourceItemVM(QuickSetupWizardDraftSpendingSource spendingSource) : this(
        spendingSource.Id,
        spendingSource.Name,
        spendingSource.SpendingSourceType switch
        {
            SpendingSourceType.Credit => "Credit",
            SpendingSourceType.BNPL => "BNPL",
            SpendingSourceType.Checking => "Checking",
            SpendingSourceType.Cash => "Cash",
            SpendingSourceType.Saving => "Savings",
            _ => "Source"
        },
        spendingSource.SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL
            ? spendingSource.SpentAmount
            : spendingSource.Balance,
        spendingSource.SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL
            ? "Spent"
            : "Balance",
        spendingSource.MaximumSpending,
        spendingSource.MinimumPayment)
    {
    }
}
