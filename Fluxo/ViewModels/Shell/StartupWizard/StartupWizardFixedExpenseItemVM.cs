using Fluxo.Core.Entities;
using Fluxo.Core.Enums;

namespace Fluxo.ViewModels.Shell.StartupWizard;

public sealed record StartupWizardFixedExpenseItemVM(
    int Id,
    string Name,
    decimal Amount,
    string CategoryLabel,
    string SpendingSourceName,
    int? DueDate)
{
    public string DueDateDisplay => DueDate.HasValue ? $"Due day {DueDate.Value}" : "No due day";

    public StartupWizardFixedExpenseItemVM(Expense expense) : this(
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
}

