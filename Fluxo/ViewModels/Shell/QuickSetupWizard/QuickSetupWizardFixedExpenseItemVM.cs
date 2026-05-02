using Fluxo.Core.Entities;
using Fluxo.Core.Enums;

namespace Fluxo.ViewModels.Shell.QuickSetupWizard;

public sealed record QuickSetupWizardFixedExpenseItemVM(
    int Id,
    string Name,
    decimal Amount,
    string CategoryLabel,
    string SpendingSourceName,
    int? DueDate)
{
    public string DueDateDisplay => DueDate.HasValue ? $"Due day {DueDate.Value}" : "No due day";

    public QuickSetupWizardFixedExpenseItemVM(Expense expense) : this(
        expense.Id,
        expense.Name,
        expense.Amount,
        expense.ExpenseCategory switch
        {
            ExpenseCategory.Needs => "Needs",
            ExpenseCategory.Wants => "Wants",
            _ => "Invest"
        },
        expense.SpendingSource?.Name ?? "No source",
        expense.RecurringDate)
    {
    }

    public QuickSetupWizardFixedExpenseItemVM(QuickSetupWizardDraftFixedExpense expense, string spendingSourceName) : this(
        expense.Id,
        expense.Name,
        expense.Amount,
        expense.Category switch
        {
            ExpenseCategory.Needs => "Needs",
            ExpenseCategory.Wants => "Wants",
            _ => "Invest"
        },
        spendingSourceName,
        expense.RecurringDate)
    {
    }
}
