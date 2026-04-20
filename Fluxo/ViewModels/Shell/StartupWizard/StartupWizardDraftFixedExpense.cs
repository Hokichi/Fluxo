using Fluxo.Core.Enums;

namespace Fluxo.ViewModels.Shell.StartupWizard;

public sealed record StartupWizardDraftFixedExpense(
    int Id,
    string Name,
    decimal Amount,
    ExpenseCategory Category,
    int SpendingSourceId,
    int RecurringDate,
    int ExpenseTagId,
    string TagName,
    bool IsActive);
