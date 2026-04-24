using Fluxo.Core.Enums;

namespace Fluxo.ViewModels.Shell.QuickSetupWizard;

public sealed record QuickSetupWizardDraftFixedExpense(
    int Id,
    string Name,
    decimal Amount,
    ExpenseCategory Category,
    int SpendingSourceId,
    int RecurringDate,
    int ExpenseTagId,
    string TagName,
    bool IsActive);
